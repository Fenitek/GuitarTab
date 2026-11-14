using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Model;
using AlphaTab.Midi;
using AlphaTab.Synth;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest dell'editor: si attiva solo con la variabile d'ambiente GS_SELFTEST=1.
/// Simula la tastiera come farebbe l'utente e scrive un resoconto in %TEMP%\gs_selftest.txt.
/// </summary>
public partial class PracticeView
{
    private bool _selfTestStarted;

    private async void MaybeRunSelfTest()
    {
        if (_selfTestStarted || Environment.GetEnvironmentVariable("GS_SELFTEST") != "1") return;
        _selfTestStarted = true;
        var report = Environment.GetEnvironmentVariable("GS_SELFTEST_REPORT") ?? Path.Combine(Path.GetTempPath(), "gs_selftest.txt");
        var log = new LiveLog(Environment.GetEnvironmentVariable("GS_SELFTEST_REPORT") != null
            ? Path.ChangeExtension(report, ".live.txt")
            : Path.Combine(Path.GetTempPath(), "gs_selftest_live.txt"));

        try
        {
            await Task.Delay(1500);
            NewFile();
            await Task.Delay(800);
            log.AppendLine($"nuova: battute={_editor.Staff!.Bars.Count} corde={_editor.StringCount} editabile={_editor.IsEditable}");

            async Task K(Key k, ModifierKeys mods = ModifierKeys.None, int wait = 120)
            {
                _testModifiers = mods;
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, k)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                RaiseEvent(args);
                _testModifiers = null;
                await Task.Delay(wait);
            }

            await VerifyAudioEdits(log);
            _editor.MarkSaved();
            NewFile();
            await Task.Delay(800);

            // Riff: corda 6 tasto 5, 7; corda 5 tasto 5, 12 (due cifre) con hammer-on.
            for (var i = 0; i < 5; i++) await K(Key.Down);
            await K(Key.D5); await K(Key.Right);
            await K(Key.D7); await K(Key.Right);
            await K(Key.Up); await K(Key.D5); await K(Key.Right);
            await K(Key.D1, wait: 50); await K(Key.D2);
            log.AppendLine($"dopo 1,2: tasto={_editor.CurrentNote?.Fret} (atteso 12)");
            await K(Key.Left); await K(Key.H);
            log.AppendLine($"hammer sulla nota prima: {_editor.CurrentNote?.IsHammerPullOrigin}");

            // Battuta 2: croma, poi effetto su tempo vuoto (deve rifiutare), annulla/ripeti.
            await K(Key.Right); await K(Key.Right);
            log.AppendLine($"cursore: battuta {_editor.BarIndex + 1} tempo {_editor.BeatIndex + 1} corda {_editor.Line}");
            await K(Key.Subtract); await K(Key.D3);
            log.AppendLine($"durata tempo corrente: {_editor.CurrentBeat?.Duration}");
            await K(Key.B);
            log.AppendLine($"bend: {_editor.CurrentNote?.HasBend}");
            await K(Key.Z, ModifierKeys.Control);
            log.AppendLine($"dopo Ctrl+Z bend: {_editor.CurrentNote?.HasBend} (atteso False)");
            await K(Key.Y, ModifierKeys.Control);
            log.AppendLine($"dopo Ctrl+Y bend: {_editor.CurrentNote?.HasBend} (atteso True)");

            // Fine della tab: oltre l'ultima battuta ne deve creare una nuova.
            await K(Key.End); await K(Key.Right, ModifierKeys.Control); await K(Key.Right, ModifierKeys.Control);
            await K(Key.End); await K(Key.Right);
            log.AppendLine($"battute dopo → in fondo: {_editor.Staff!.Bars.Count} (attese 5)");
            await K(Key.Delete, ModifierKeys.Control | ModifierKeys.Shift);
            log.AppendLine($"battute dopo elimina battuta: {_editor.Staff!.Bars.Count} (attese 4)");

            // Inserisci / elimina tempo.
            await K(Key.Home); await K(Key.Left, ModifierKeys.Control);
            var before = _editor.CurrentVoice!.Beats.Count;
            await K(Key.Insert);
            var afterIns = _editor.CurrentVoice!.Beats.Count;
            await K(Key.Delete, ModifierKeys.Control);
            log.AppendLine($"tempi: {before} → ins {afterIns} → canc {_editor.CurrentVoice!.Beats.Count}");

            // --- Selezione + copia/incolla di battute intere.
            _editor.MoveTo(0, 0, 6);
            await K(Key.End, ModifierKeys.Shift);
            log.AppendLine($"selezione battuta 1: {_editor.SelectedBeats().Count} tempi, battute intere={_editor.SelectionIsWholeBars(out _, out _)}");
            await K(Key.C, ModifierKeys.Control);
            await K(Key.Escape);
            var barsBefore = _editor.Staff!.Bars.Count;
            await K(Key.V, ModifierKeys.Control, 300);
            var pasted = _editor.Staff!.Bars[1].Voices[0].Beats;
            log.AppendLine($"incolla battuta: battute {barsBefore} → {_editor.Staff!.Bars.Count}, battuta 2 = {string.Join(" ", pasted.Select(b => b.Notes.Count > 0 ? b.Notes[0].Fret.ToString() : "r"))} (attesa 5 7 5 12)");
            log.AppendLine($"hammer copiato: {pasted[2].Notes[0].IsHammerPullOrigin}");

            // --- Terzina su tre tempi selezionati.
            _editor.MoveTo(2, 0, 3);
            await K(Key.Subtract);
            await K(Key.Right, ModifierKeys.Shift); await K(Key.Right, ModifierKeys.Shift);
            await K(Key.D3, ModifierKeys.Control);
            log.AppendLine($"terzina: {string.Join(",", _editor.SelectedBeats().Select(TabEditor.TupletLabel))}");
            await K(Key.Escape);

            // --- Effetti nuovi.
            _editor.MoveTo(0, 0, 6);
            await K(Key.N); await K(Key.G);
            log.AppendLine($"armonico naturale={_editor.CurrentNote?.HarmonicType} ghost={_editor.CurrentNote?.IsGhost}");
            _editor.ToggleEffect(NoteEffect.PickDown);
            _editor.ToggleEffect(NoteEffect.BendRelease);
            log.AppendLine($"pennata={_editor.CurrentBeat?.PickStroke} bend={_editor.CurrentNote?.BendType}");
            await Task.Delay(300);

            // --- Manico: clic su corda 1 tasto 3 nel tempo corrente.
            _editor.ToggleFretAt(1, 3);
            log.AppendLine($"manico: note nel tempo = {string.Join(" ", _editor.CurrentBeat!.Notes.Select(n => $"c{_editor.LineOf((int)n.String)}:{n.Fret}"))}");

            // --- Proprietà battuta: BPM, ritornello, sezione.
            _editor.MoveTo(1, 0, 1);
            _editor.SetBarInfo(new BarInfo(140, 140, 4, 4, true, 3, "Ritornello"));
            await Task.Delay(300);
            var mb = _editor.Score!.MasterBars[1];
            log.AppendLine($"battuta 2: BPM={_editor.TempoAt(1)} ritornello inizio={mb.IsRepeatStart} volte={mb.RepeatCount} sezione={mb.Section?.Text}");

            // --- Correggi battuta incompleta (battuta 3 ha una terzina e una croma).
            _editor.MoveTo(2, 0, 1);
            _editor.BarFill(out var u1, out var t1);
            await K(Key.F, ModifierKeys.Control);
            _editor.BarFill(out var u2, out var t2);
            log.AppendLine($"correggi battuta: {u1}/{t1} → {u2}/{t2}");

            // --- Nuova traccia di basso e accordatura Drop D con capotasto sulla chitarra.
            var bassTuning = Tuning.GetDefaultTuningFor(4)!.Tunings.ToList();
            _editor.AddTrack("Basso", 33, bassTuning, true);
            await Task.Delay(400);
            log.AppendLine($"tracce: {_editor.Score!.Tracks.Count} corrente={_editor.Track?.Name} corde={_editor.StringCount} pannello tracce={TrackRows.Children.Count}");
            _editor.MoveTo(0, 0, 4);
            await K(Key.D3);
            log.AppendLine($"nota sul basso: {_editor.CurrentNote?.Fret}");
            _editor.SelectTrack(0);
            var dropD = new List<double> { 64, 59, 55, 50, 45, 38 };
            _editor.SetTrackProperties("Chitarra", 30, dropD, "Drop D", 2);
            log.AppendLine($"chitarra: accordatura={_editor.Staff!.StringTuning.Name} capo={_editor.Staff!.Capo} suono={_editor.Track!.PlaybackInfo.Program}");

            _editor.SetTitle("Riff di prova");
            _editor.SetArtist("GuitarStudio");
            await Task.Delay(600);

            // Avviso quando manca la nota di arrivo di un hammer-on, ed esportazione MIDI.
            _editor.MoveTo(3, 0, 2);
            await K(Key.D5); await K(Key.H);
            log.AppendLine($"hammer senza nota di arrivo: avviso={(_editor.LinkWarning(NoteEffect.HammerPull) != null)} (atteso True) toast={Toast.Visibility} (atteso Visible) applicato={_editor.CurrentNote?.IsHammerPullOrigin} (atteso False)");
            log.AppendLine($"midi esportato: {_editor.ExportMidi().Length} byte");

            log.AppendLine("--- alphaTex ---");
            log.AppendLine(_editor.ExportAlphaTex());

            // Salvataggio .gp e rilettura.
            var gp = Path.Combine(Path.GetTempPath(), "gs_selftest.gp");
            File.WriteAllBytes(gp, _editor.ExportGp());
            var reloaded = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(gp)), Api.Settings);
            log.AppendLine($"--- .gp riletto: titolo={reloaded.Title} artista={reloaded.Artist} battute={reloaded.MasterBars.Count} tracce={reloaded.Tracks.Count} " +
                           $"note battuta 1={reloaded.Tracks[0].Staves[0].Bars[0].Voices[0].Beats.Sum(b => b.Notes.Count)} " +
                           $"capo={reloaded.Tracks[0].Staves[0].Capo} ritornello={reloaded.MasterBars[1].RepeatCount} sezione={reloaded.MasterBars[1].Section?.Text} " +
                           $"terzina={reloaded.Tracks[0].Staves[0].Bars[2].Voices[0].Beats[0].TupletNumerator}");

            // Riproduzione dopo le modifiche.
            Api.Stop();
            await Task.Delay(300);
            Api.Play();
            await Task.Delay(2000);
            log.AppendLine($"riproduzione: stato={(_playing ? "in corso" : "ferma")} tempo={TimeText.Text}");
            Api.Stop();
            _editor.MarkSaved();
            _editor.MoveTo(0, 2, 5);
            await Task.Delay(1200);
            log.AppendLine($"cursore finale: {CursorText.Text} | traccia: {LcdTrack.Text} | play: {_playing}");
            log.AppendLine("OK");

            // GS_SELFTEST_UI=1: lascia aperti guida, menu e avviso per poterli fotografare.
            if (Environment.GetEnvironmentVariable("GS_SELFTEST_UI") == "1")
            {
                ShowInspector(TabTrack);
                ShowToast("Lo slide parte da questa nota e arriva alla nota successiva sulla stessa corda, che qui manca.\nScrivi prima la nota di arrivo.", ToastKind.Warning);
                await Task.Delay(400);
                TrackSoundCombo.IsDropDownOpen = true;
                (Window.GetWindow(this) as MainWindow)?.ShowHelp();
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("ECCEZIONE: " + ex);
        }
        File.WriteAllText(report, log.ToString());
    }

    private async Task VerifyAudioEdits(LiveLog log)
    {
        MidiFile? loadedMidi = null;
        void CaptureMidi(MidiFile file) => loadedMidi = file;
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("audio OK: " + message);
        }

        Api.MidiLoad.On(CaptureMidi);
        try
        {
            _editor.MoveTo(0, 0, 6);
            _editor.SetFret(5);
            await Task.Delay(300);
            Require(loadedMidi?.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 45) == true,
                "la nota scritta nella tablatura vuota arriva al MIDI del lettore");

            _editor.SetStartTempo(75);
            await Task.Delay(300);
            Require(Math.Abs(Api.EndTime - 12800) < 2, "il lettore usa il nuovo BPM (4 battute a 75 BPM = 12,8 s)");

            _editor.SetFret(7);
            await Task.Delay(300);
            Require(loadedMidi!.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 47) &&
                    !loadedMidi.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 45),
                "il lettore sostituisce la vecchia nota dopo una modifica");

            var staff = _editor.Staff!;
            _editor.SetTrackProperties("Chitarra", 25, staff.Tuning.ToList(), staff.StringTuning.Name, 0);
            await Task.Delay(300);
            Require(loadedMidi.Events.OfType<ProgramChangeEvent>().Any(p => p.Program == 25),
                "il cambio di suono raggiunge il sintetizzatore");

            var exporter = await Api.ExportAudio(new AudioExportOptions { MetronomeVolume = 0 });
            var chunk = await exporter.Render(500) ?? throw new InvalidOperationException("Nessun campione audio generato");
            var samples = chunk.Samples;
            double peak = 0;
            for (var i = 0; i < samples.Length; i++)
                peak = Math.Max(peak, Math.Abs(samples[i]));
            Require(peak > 0.0001, $"il sintetizzatore produce audio non silenzioso (picco {peak:0.0000})");

            Api.Stop();
            Play_Click(this, new RoutedEventArgs());
            await Task.Delay(700);
            Require(_playing && Api.TimePosition > 0, "Play avvia il brano modificato e il tempo avanza");
            Api.Stop();
        }
        finally
        {
            Api.MidiLoad.Off(CaptureMidi);
        }
    }

    private sealed class LiveLog
    {
        private readonly StringBuilder _sb = new();
        private readonly string _path;
        public LiveLog(string path) { _path = path; File.WriteAllText(path, ""); }
        public void AppendLine(string? s = null) { _sb.AppendLine(s); File.AppendAllText(_path, s + Environment.NewLine); }
        public override string ToString() => _sb.ToString();
    }

    private ModifierKeys? _testModifiers;

    private ModifierKeys CurrentModifiers => _testModifiers ?? Keyboard.Modifiers;
}
