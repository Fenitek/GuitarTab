using System.IO;
using System.Windows;
using System.Windows.Input;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Model;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest dei segni di notazione (dinamiche, abbellimenti, accordi, testo, finali alternativi).
/// I tasti sono eventi WPF inviati alla vista, mai input di sistema.
/// </summary>
public partial class PracticeView
{
    private async Task Press(Key key, ModifierKeys mods = ModifierKeys.None, int wait = 120)
    {
        _testModifiers = mods;
        RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
        _testModifiers = null;
        await Task.Delay(wait);
    }

    /// <summary>Attende il MIDI corrente del brano e restituisce quello ricevuto dal lettore.</summary>
    private async Task<MidiFile> CurrentMidi(Func<MidiFile, bool>? condition = null)
    {
        MidiFile? loaded = null;
        void Capture(MidiFile file) => loaded = file;
        Api.MidiLoad.On(Capture);
        try
        {
            ScheduleMidiReload();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((!_midiReady || loaded == null || condition?.Invoke(loaded) == false) && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            return loaded ?? throw new InvalidOperationException("Il lettore non ha ricevuto il MIDI aggiornato.");
        }
        finally
        {
            Api.MidiLoad.Off(Capture);
        }
    }

    private Score SaveAndReopen(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gs_selftest_{name}.gp");
        File.WriteAllBytes(path, _editor.ExportGp());
        var score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(path)), Api.Settings);
        File.Delete(path);
        return score;
    }

    private async Task VerifyDynamicsAndGrace(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("dinamiche/abbellimenti OK: " + message);
        }

        ShowInspector(TabBar);
        _editor.MoveTo(0, 0, 1);
        foreach (var key in new[] { Key.D5, Key.D7, Key.D8, Key.D5 })
        {
            await Press(key); await Press(Key.Right);
        }
        _editor.MoveTo(0, 1, 1);
        await Press(Key.D, ModifierKeys.Shift); await Press(Key.D, ModifierKeys.Shift);
        var beats = _editor.CurrentVoice!.Beats;
        Require(beats[0].Dynamics == DynamicValue.F && beats.Skip(1).All(b => b.Dynamics == DynamicValue.MP),
            "Maiusc+D due volte: mp dal secondo tempo in poi, f sul primo");
        Require(BeatDynamicsText.Text.StartsWith("mp", StringComparison.Ordinal) &&
                _dynamicToggles.Single(t => t.IsChecked == true).Tag is DynamicValue.MP && CursorText.Text.Contains("· mp", StringComparison.Ordinal),
            "valore mostrato in proprietà, tavolozza e riga di stato");
        await Press(Key.D);
        Require(_editor.CurrentBeat!.Dynamics == DynamicValue.MF, "D rende la dinamica più forte (mf)");

        _editor.MoveTo(0, 2, 1);
        await Press(Key.G, ModifierKeys.Control);
        Require(_editor.CurrentBeat!.GraceType == GraceType.BeforeBeat && GraceBeforeToggle.IsChecked == true &&
                _editor.CurrentNote!.Fret == 6, "Ctrl+G inserisce un abbellimento prima del tempo, un tono sotto l'8");
        _lastDigit = null; await Press(Key.D7);
        Require(_editor.CurrentNote!.Fret == 7, "una cifra cambia il tasto dell'abbellimento");
        _editor.MoveTo(0, 4, 1);
        await Press(Key.G, ModifierKeys.Control | ModifierKeys.Shift);
        Require(_editor.CurrentBeat!.GraceType == GraceType.OnBeat && GraceOnToggle.IsChecked == true,
            "Ctrl+Maiusc+G inserisce un abbellimento sul tempo");
        Require(_editor.BarFill(out var used, out var total) && Math.Abs(used - total) < 0.5,
            "gli abbellimenti non cambiano la durata della battuta");
        await Press(Key.Z, ModifierKeys.Control);
        Require(_editor.CurrentVoice!.Beats.Count(b => b.GraceType != GraceType.None) == 1, "Ctrl+Z toglie l'ultimo abbellimento");
        await Press(Key.Y, ModifierKeys.Control);
        Require(_editor.CurrentVoice!.Beats.Count(b => b.GraceType != GraceType.None) == 2, "Ctrl+Y lo rimette");

        // Pitch: corda 1 a vuoto = 64. Note principali 5 (69), 7 (71), 8 (72), 5 (69).
        var midi = await CurrentMidi(m => m.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 71 && n.Tick < 3840));
        var notes = midi.Events.OfType<NoteOnEvent>().Where(n => n.Tick < 3840).OrderBy(n => n.Tick).ToList();
        log.AppendLine("MIDI battuta 1: " + string.Join(" ", notes.Select(n => $"{n.NoteKey}@{n.Tick}v{n.NoteVelocity}")));
        var first = notes.First(n => n.NoteKey == 69);
        var second = notes.First(n => n.NoteKey == 71);
        Require(second.NoteVelocity < first.NoteVelocity, "il lettore suona mf più piano di f");
        var beforeGrace = notes.Single(n => n.NoteKey == 71 && n.Tick > second.Tick);
        Require(beforeGrace.Tick < 1920 && notes.Any(n => n.NoteKey == 72 && n.Tick == 1920),
            "l'abbellimento prima del tempo suona prima dell'8, che resta sul terzo movimento");
        Require(notes.Any(n => n.NoteKey == 67 && n.Tick == 2880) && notes.Any(n => n.NoteKey == 69 && n.Tick > 2880),
            "l'abbellimento sul tempo cade sul quarto movimento e ritarda la nota principale");

        var reopened = SaveAndReopen("dinamiche");
        var voice = reopened.Tracks[0].Staves[0].Bars[0].Voices[0];
        Require(voice.Beats[0].Dynamics == DynamicValue.F && voice.Beats[1].Dynamics == DynamicValue.MF &&
                voice.Beats.Count(b => b.GraceType == GraceType.BeforeBeat) == 1 &&
                voice.Beats.Count(b => b.GraceType == GraceType.OnBeat) == 1,
            "salvataggio .gp e riapertura conservano dinamiche e abbellimenti");

        Play_Click(this, new RoutedEventArgs());
        await Task.Delay(800);
        Require(_playing, "la riproduzione parte con dinamiche e abbellimenti");
        Api.Stop();
        await Task.Delay(200);
    }

    /// <summary>Scena per le foto: dinamiche diverse, i due abbellimenti e il pannello Battuta.</summary>
    private void PrepareNotationScene()
    {
        var importer = new AlphaTexImporter();
        importer.InitFromString("\\title \"Segni di notazione\" . " +
            string.Join(" | ", Enumerable.Repeat(":4 5.3 7.3 8.2 5.1", 6)), Api.Settings);
        _editor.Attach(importer.ReadScore(), 0);
        _editor.MoveTo(0, 2, 1); _editor.SetDynamics(DynamicValue.PP);
        _editor.MoveTo(1, 0, 1); _editor.SetDynamics(DynamicValue.MF);
        _editor.MoveTo(2, 0, 1); _editor.SetDynamics(DynamicValue.FFF);
        _editor.MoveTo(0, 1, 3); _editor.ToggleGrace(GraceType.BeforeBeat);
        _editor.MoveTo(1, 3, 1); _editor.ToggleGrace(GraceType.OnBeat);
        _editor.MarkSaved();
    }

    private async Task CaptureNotationScene(Window main, string prefix, string name, Action prepare)
    {
        var original = _editor.Score!;
        prepare();
        ShowInspector(TabBar);
        Render(); UpdateEditUi();
        await VisualCapture.Paint();
        ScrollView!.ScrollToTop();
        await VisualCapture.Paint();
        _visualCapture!.Save(main, prefix + name);
        _editor.Attach(original, 0); _editor.MarkSaved(); Render();
        await VisualCapture.Paint();
    }
}
