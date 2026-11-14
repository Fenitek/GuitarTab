using System.Diagnostics;
using System.Windows.Threading;
using AlphaTab.Model;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private DispatcherTimer? _midiReloadTimer;
    private readonly Queue<(Score? Score, int Revision)> _midiLoads = new();
    private double _lastDeferredMidiMs;
    private int _midiGenerationCount;

    private void ScheduleMidiReload()
    {
        _midiReady = false;
        _midiReloadTimer ??= CreateMidiReloadTimer();
        _midiReloadTimer.Stop();
        _midiReloadTimer.Start();
    }

    private DispatcherTimer CreateMidiReloadTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_editor.Score == null) return;
            var sw = Stopwatch.StartNew();
            Api.LoadMidiForScore();
            _lastDeferredMidiMs = sw.Elapsed.TotalMilliseconds;
        };
        return timer;
    }

    private void OnMidiLoad()
    {
        _midiReady = false;
        _midiGenerationCount++;
        // Il sintetizzatore processa i caricamenti in ordine. Un MidiLoaded vecchio non
        // deve abilitare anteprime sul modello/canali di una revisione successiva.
        _midiLoads.Enqueue((Api.Score, _revision));
    }

    private void OnMidiLoaded()
    {
        if (!_midiLoads.TryDequeue(out var loaded)) return;
        _midiReady = _midiReloadTimer?.IsEnabled != true &&
                     ReferenceEquals(loaded.Score, _editor.Score) && loaded.Revision == _revision;
        if (_midiReady && !_playing && _editor.CurrentBeat is { } beat)
            Api.TickPosition = beat.AbsolutePlaybackStart;
    }

    private void CancelPendingMidiReload()
    {
        CancelPendingEditorRender();
        _midiReloadTimer?.Stop();
        _midiReady = false;
    }
}
