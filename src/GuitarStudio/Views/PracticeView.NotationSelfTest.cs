using System.IO;
using System.Windows;
using System.Windows.Input;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Model;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;
using GuitarStudio.Ui;

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

    /// <summary>Compila un dialogo dell'autotest come farebbe l'utente e sceglie la risposta.</summary>
    private static Func<Window, MessageBoxResult> FillDialog(MessageBoxResult answer, params (string Name, object Value)[] fields) => window =>
    {
        foreach (var (name, value) in fields)
            switch (LogicalTreeHelper.FindLogicalNode(window, name))
            {
                case System.Windows.Controls.TextBox box: box.Text = (string)value; break;
                case System.Windows.Controls.CheckBox check: check.IsChecked = (bool)value; break;
                default: throw new InvalidOperationException($"Campo {name} assente nel dialogo «{window.Title}».");
            }
        return answer;
    };

    private async Task VerifyChordsAndText(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("accordi/testo OK: " + message);
        }

        var previousPresenter = Dialogs.SelfTestPresenter;
        try
        {
            ShowInspector(TabBar);
            // Do aperto: x 3 2 0 1 0 dalla corda più grave.
            foreach (var (line, fret) in new[] { (1, 0), (2, 1), (3, 0), (4, 2), (5, 3) })
            {
                _editor.MoveTo(0, 0, line); _editor.SetFret(fret);
            }
            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.Yes, ("ChordNameBox", "Do"));
            await Press(Key.C);
            Require(_editor.CurrentChord is { Name: "Do", ShowDiagram: true } chord &&
                    TabEditor.FormatFrets(chord.FretsLowToHigh) == "x 3 2 0 1 0",
                "C apre il dialogo con il diagramma proposto dalle note e mette «Do»");
            Require(BeatChordText.Text.StartsWith("Do", StringComparison.Ordinal) && CursorText.Text.Contains("accordo Do", StringComparison.Ordinal),
                "accordo mostrato nel pannello Battuta e nella riga di stato");

            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.Yes, ("ChordNameBox", "Do"), ("ChordFretsBox", "3 2"));
            var refused = false;
            try { await Press(Key.C); } catch (InvalidOperationException ex) when (ex.Message.Contains("diagramma vuole 6")) { refused = true; }
            Require(refused && _editor.CurrentChord!.Name == "Do", "un diagramma con troppi pochi valori viene rifiutato con un messaggio");

            _editor.MoveTo(0, 2, 1);
            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.Yes, ("ChordNameBox", "Sol"), ("ChordDiagramCheck", false));
            ChordButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(150);
            Require(_editor.CurrentChord is { Name: "Sol", ShowDiagram: false }, "pulsante Accordo… della tavolozza: accordo con il solo nome");

            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.Yes, ("BeatTextBox", "rallentando"));
            await Press(Key.T, ModifierKeys.Shift);
            Require(_editor.CurrentBeat!.Text == "rallentando" && BeatTextText.Text.Contains("rallentando"),
                "Maiusc+T mette il testo sopra il tempo e il pannello lo mostra");
            Require(_editor.CurrentNote == null || _editor.CurrentNote.IsTieDestination == false, "Maiusc+T non applica la legatura");

            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.No);
            await Press(Key.T, ModifierKeys.Shift);
            Require(string.IsNullOrEmpty(_editor.CurrentBeat!.Text), "Togli testo lo rimuove");
            await Press(Key.Z, ModifierKeys.Control);
            Require(_editor.CurrentBeat!.Text == "rallentando", "Ctrl+Z rimette il testo");
            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.Cancel, ("BeatTextBox", "non applicare"));
            await Press(Key.T, ModifierKeys.Shift);
            Require(_editor.CurrentBeat!.Text == "rallentando", "Annulla nel dialogo non cambia niente");

            _editor.MoveTo(0, 0, 1);
            Dialogs.SelfTestPresenter = FillDialog(MessageBoxResult.No);
            await Press(Key.C);
            Require(_editor.CurrentChord == null, "Togli accordo lo rimuove");
            await Press(Key.Z, ModifierKeys.Control);
            await Press(Key.Y, ModifierKeys.Control);
            await Press(Key.Z, ModifierKeys.Control);
            Require(_editor.CurrentChord?.Name == "Do", "annulla e ripeti dell'accordo");

            var reopened = SaveAndReopen("accordi");
            var beats = reopened.Tracks[0].Staves[0].Bars[0].Voices[0].Beats;
            Require(beats[0].Chord is { Name: "Do", ShowDiagram: true } && beats[2].Chord is { Name: "Sol", ShowDiagram: false } &&
                    beats[2].Text == "rallentando", "salvataggio .gp e riapertura conservano accordi, diagramma e testo");
            Render(); await Task.Delay(400);
        }
        finally
        {
            Dialogs.SelfTestPresenter = previousPresenter;
        }
    }

    private void PrepareChordScene()
    {
        var importer = new AlphaTexImporter();
        importer.InitFromString("\\title \"Accordi e testo\" . " +
            string.Join(" | ", Enumerable.Repeat(":4 (0.1 1.2 0.3 2.4 3.5) (0.1 1.2 0.3 2.4 3.5) (3.1 0.2 0.3 0.4 2.5 3.6) (3.1 0.2 0.3 0.4 2.5 3.6)", 4)), Api.Settings);
        _editor.Attach(importer.ReadScore(), 0);
        _editor.MoveTo(0, 0, 1); _editor.SetChord(_editor.SuggestChord() with { Name = "Do" });
        _editor.MoveTo(0, 2, 1); _editor.SetChord(_editor.SuggestChord() with { Name = "Sol" });
        _editor.MoveTo(1, 0, 1); _editor.SetChord(_editor.SuggestChord() with { Name = "Do", ShowDiagram = false });
        _editor.MoveTo(0, 0, 1); _editor.SetBeatText("Strofa");
        _editor.MoveTo(1, 2, 1); _editor.SetBeatText("rallentando");
        _editor.MoveTo(0, 2, 1);
        _editor.MarkSaved();
    }

    /// <summary>Fotografa un dialogo sopra la finestra e lo chiude con Annulla.</summary>
    private Func<Window, MessageBoxResult> CaptureDialog(Window main, string name) => window =>
    {
        window.ShowActivated = false;
        window.Show();
        window.Left = main.Left + (main.ActualWidth - window.ActualWidth) / 2;
        window.Top = main.Top + (main.ActualHeight - window.ActualHeight) / 2;
        VisualCapture.PaintSynchronously();
        _visualCapture!.Save(main, name, window);
        window.Close();
        return MessageBoxResult.Cancel;
    };

    private async Task CaptureChordScene(Window main, string prefix)
    {
        await CaptureNotationScene(main, prefix, "accordi-testo", () =>
        {
            PrepareChordScene();
            var previous = Dialogs.SelfTestPresenter;
            Dialogs.SelfTestPresenter = CaptureDialog(main, prefix + "dialogo-accordo");
            EditChord();
            _editor.MoveTo(1, 2, 1);
            Dialogs.SelfTestPresenter = CaptureDialog(main, prefix + "dialogo-testo");
            EditBeatText();
            Dialogs.SelfTestPresenter = previous;
            _editor.MoveTo(0, 2, 1);
        });
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
