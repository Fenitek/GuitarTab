using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GuitarStudio.Audio;

public sealed record PreviewMeasurement(long Id, double SamplesMs, double DeviceMs);

/// <summary>WASAPI condiviso, sempre pronto, su un thread proprio. Nessun oggetto del
/// modello passa al thread audio; anche creazione e chiusura del client avvengono lì.</summary>
public sealed class NotePreviewPlayer : IDisposable
{
    private sealed record Command(long Id, int Program, int Key, int Velocity, int Volume, int Pan, long RequestedAt, bool Stop);
    private sealed record Target(long Id, long RequestedAt, double ClockSeconds, double SamplesMs);
    private readonly EventWaitHandle _stop = new(false, EventResetMode.ManualReset);
    private readonly ManualResetEventSlim _started = new();
    private readonly Thread _thread;
    private readonly ConcurrentQueue<PreviewMeasurement> _measurements = new();
    private PreviewRenderer? _renderer;
    private Command? _command;
    private Command? _sounding;
    private Target? _target;
    private long _id;
    private volatile bool _disposed;
    private volatile Exception? _audioError;
    public bool IsReady => !_disposed && AudioError == null && Volatile.Read(ref _renderer) != null;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(PreviewRenderer.SampleRate, 2);
    public double BufferMs { get; private set; }
    public bool TryGetMeasurement(out PreviewMeasurement? measurement) => _measurements.TryDequeue(out measurement);
    public Exception? AudioError => _audioError;

    public NotePreviewPlayer()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Anteprima note", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        if (!_started.Wait(TimeSpan.FromSeconds(5)) || AudioError != null)
        {
            Dispose();
            throw new InvalidOperationException("Impossibile avviare l'uscita delle anteprime.", AudioError);
        }
    }

    public void SetRenderer(PreviewRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stop(); Volatile.Write(ref _renderer, renderer);
    }

    public long Play(int program, int key, int velocity, int volume, int pan, long requestedAt)
    {
        if (!IsReady || program is < 0 or > 127 || key is < 0 or > 127) return 0;
        var id = Interlocked.Increment(ref _id);
        Interlocked.Exchange(ref _command, new Command(id, program, key, velocity, volume, pan, requestedAt, false));
        return id;
    }

    public void Stop()
    {
        var id = Interlocked.Increment(ref _id);
        Interlocked.Exchange(ref _target, null);
        Interlocked.Exchange(ref _command, new Command(id, 0, 0, 0, 0, 0, 0, true));
    }

    private void Run()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            using var client = device.AudioClient;
            client.Initialize(AudioClientShareMode.Shared,
                AudioClientStreamFlags.EventCallback | AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality,
                0, 0, WaveFormat, Guid.Empty); // Buffer minimo del motore per WASAPI condiviso a eventi.
            using var available = new EventWaitHandle(false, EventResetMode.AutoReset);
            client.SetEventHandle(available.SafeWaitHandle.DangerousGetHandle());
            var render = client.AudioRenderClient;
            var clock = client.AudioClockClient;
            var frames = client.BufferSize;
            BufferMs = frames * 1000.0 / PreviewRenderer.SampleRate;
            var samples = new float[frames * 2];
            render.GetBuffer(frames); render.ReleaseBuffer(frames, AudioClientBufferFlags.Silent);
            client.Start(); _started.Set();
            try
            {
                WaitHandle[] handles = [_stop, available];
                while (WaitHandle.WaitAny(handles, 5) != 0)
                {
                    var target = Volatile.Read(ref _target);
                    if (target != null)
                    {
                        var seconds = clock.AdjustedPosition / (double)clock.Frequency;
                        if (target.Id == Volatile.Read(ref _id) && seconds >= target.ClockSeconds &&
                            ReferenceEquals(Interlocked.CompareExchange(ref _target, null, target), target))
                        {
                            // Sottrae solo il ritardo di osservazione misurato dall'AudioClock.
                            var latency = Stopwatch.GetElapsedTime(target.RequestedAt).TotalMilliseconds -
                                (seconds - target.ClockSeconds) * 1000;
                            _measurements.Enqueue(new PreviewMeasurement(target.Id, target.SamplesMs, latency));
                            while (_measurements.Count > 128) _measurements.TryDequeue(out _);
                        }
                    }
                    var count = frames - client.CurrentPadding;
                    if (count == 0) continue;
                    Fill(samples, count * 2);
                    var padding = client.CurrentPadding;
                    var startSeconds = clock.AdjustedPosition / (double)clock.Frequency +
                        padding / (double)PreviewRenderer.SampleRate;
                    var pointer = render.GetBuffer(count);
                    Marshal.Copy(samples, 0, pointer, count * 2);
                    render.ReleaseBuffer(count, AudioClientBufferFlags.None);
                    if (_sounding is not { } note || note.Id != Volatile.Read(ref _id)) continue;
                    for (var i = 0; i < count * 2; i++)
                    {
                        if (Math.Abs(samples[i]) < 0.0001) continue;
                        Interlocked.Exchange(ref _target, new Target(note.Id, note.RequestedAt,
                            startSeconds + (i / 2 + 1) / (double)PreviewRenderer.SampleRate,
                            Stopwatch.GetElapsedTime(note.RequestedAt).TotalMilliseconds));
                        _sounding = null; break;
                    }
                }
            }
            finally { client.Stop(); }
        }
        catch (Exception ex) { _audioError = ex; }
        finally { _started.Set(); }
    }

    private void Fill(float[] samples, int count)
    {
        var renderer = Volatile.Read(ref _renderer);
        var command = Interlocked.Exchange(ref _command, null);
        if (command != null)
        {
            _sounding = command.Stop ? null : command;
            if (command.Stop) renderer?.Stop();
            else renderer?.Start(command.Program, command.Key, command.Velocity, command.Volume, command.Pan);
        }
        if (renderer == null) Array.Clear(samples, 0, count);
        else renderer.Read(samples, 0, count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _stop.Set();
        _thread.Join();
        _stop.Dispose(); _started.Dispose();
    }
}
