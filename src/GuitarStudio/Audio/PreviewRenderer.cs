using AlphaTab.Core.EcmaScript;
using AlphaTab.Midi;
using AlphaTab.Synth;

namespace GuitarStudio.Audio;

/// <summary>Sintetizzatore privato delle anteprime: nessun riferimento a Score o al player della vista.
/// Tutte le chiamate dopo il caricamento appartengono al solo thread audio.</summary>
public sealed class PreviewRenderer
{
    public const int SampleRate = 48000;
    private readonly AlphaSynthAudioExporter _synth;
    private Float32Array? _chunk;
    private int _position;
    private bool _active;
    private readonly MidiFile _silence = CreateSilence();

    private static MidiFile CreateSilence()
    {
        var midi = new MidiFile();
        midi.AddEvent(new ControlChangeEvent(0, 0, 0, ControllerType.AllSoundOff, 0));
        midi.AddEvent(new EndOfTrackEvent(0, 960));
        return midi;
    }

    public PreviewRenderer(byte[] soundFont)
    {
        _synth = new AlphaSynthAudioExporter(new AudioExportOptions
        {
            SampleRate = SampleRate, MasterVolume = 0.8, MetronomeVolume = 0
        });
        // Precarica i campioni di tutti i programmi prima di abilitare la tastiera audio.
        // L'exporter pubblico carica solo i preset citati dal MIDI già assegnato.
        var warmup = new MidiFile();
        for (var program = 0; program < 128; program++)
            warmup.AddEvent(new ProgramChangeEvent(0, 0, 0, program));
        warmup.AddEvent(new EndOfTrackEvent(0, 1));
        _synth.LoadMidiFile(warmup);
        _synth.LoadSoundFont(new Uint8Array(soundFont));
        _synth.Setup();
        Start(25, 64);
        Read(new float[2048], 0, 2048);
        Stop();
    }

    public static MidiFile CreateNote(int program, int key, int velocity = 96, int volume = 127, int pan = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(program);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(program, 127);
        ArgumentOutOfRangeException.ThrowIfNegative(key);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key, 127);
        var midi = new MidiFile();
        // Sempre il canale privato 0, inizializzato dal MIDI breve, anche per una traccia nuova.
        midi.AddEvent(new ControlChangeEvent(0, 0, 0, ControllerType.AllSoundOff, 0));
        midi.AddEvent(new ProgramChangeEvent(0, 0, 0, program));
        midi.AddEvent(new ControlChangeEvent(0, 0, 0, ControllerType.VolumeCoarse, Math.Clamp(volume, 0, 127)));
        midi.AddEvent(new ControlChangeEvent(0, 0, 0, ControllerType.PanCoarse, Math.Clamp(pan, 0, 127)));
        midi.AddEvent(new NoteOnEvent(0, 0, 0, key, Math.Clamp(velocity, 1, 127)));
        midi.AddEvent(new NoteOffEvent(0, 480, 0, key, 0));
        midi.AddEvent(new EndOfTrackEvent(0, 481));
        return midi;
    }

    public void Start(int program, int key, int velocity = 96, int volume = 127, int pan = 64)
    {
        Stop();
        // AllSoundOff in alphaTab applica un rilascio breve, non elimina i campioni già
        // attivi. Lo completa fuori dall'uscita prima della nuova nota (nessuna attesa).
        _synth.LoadMidiFile(_silence); _synth.Setup(); _synth.Render(30);
        _synth.LoadMidiFile(CreateNote(program, key, velocity, volume, pan));
        _synth.Setup();
        _active = true;
    }

    public void Stop() { _active = false; _chunk = null; _position = 0; }

    public void Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        while (_active && count > 0)
        {
            if (_chunk == null || _position >= _chunk.Length)
            {
                _chunk = _synth.Render(10)?.Samples;
                _position = 0;
                if (_chunk == null || _chunk.Length == 0) { Stop(); break; }
            }
            var copy = Math.Min(count, (int)_chunk.Length - _position);
            for (var i = 0; i < copy; i++) buffer[offset + i] = (float)_chunk[_position + i];
            _position += copy; offset += copy; count -= copy;
        }
    }
}
