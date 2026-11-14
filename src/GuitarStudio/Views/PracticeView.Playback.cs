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
    private int _previewRequested;
    private int _previewPlayed;
    private sealed record PendingPreview(Score Score, Note Note, int Revision, long RequestedAt);
    private PendingPreview? _pendingPreview;
    private double _lastPreviewLatencyMs;
    private double _lastPreviewFret;

    private void TryPlayPendingPreview()
    {
        if (_pendingPreview is not { } pending) return;
        if (_playing || pending.Revision != _revision || !ReferenceEquals(pending.Score, _editor.Score))
        {
            _pendingPreview = null;
            return;
        }
        var note = pending.Note;
        var beat = note.Beat;
        var voice = beat.Voice;
        var bar = voice.Bar;
        var staff = bar.Staff;
        var track = staff.Track;
        if (!pending.Score.Tracks.Contains(track) || !track.Staves.Contains(staff) || !staff.Bars.Contains(bar) ||
            !bar.Voices.Contains(voice) || !voice.Beats.Contains(beat) || !beat.Notes.Contains(note))
        {
            _pendingPreview = null;
            return;
        }
        if (!_soundFontReady || !_midiReady) return;
        _pendingPreview = null;
        // Unico punto PlayNote: sia i canali sia la revisione MIDI devono essere pronti.
        try
        {
            Api.PlayNote(note);
            _previewPlayed++;
            _lastPreviewFret = note.Fret;
            _lastPreviewLatencyMs = Stopwatch.GetElapsedTime(pending.RequestedAt).TotalMilliseconds;
        }
        catch (Exception ex) { App.Log(ex); }
    }

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
        if (_midiReady) TryPlayPendingPreview();
    }

    private void CancelPendingMidiReload()
    {
        CancelPendingEditorRender();
        _midiReloadTimer?.Stop();
        _midiReady = false;
        _pendingPreview = null;
    }
}
