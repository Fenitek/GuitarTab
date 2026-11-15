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
using Duration = AlphaTab.Model.Duration;

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
        var initialErrors = App.UnhandledErrorCount;
        var report = Environment.GetEnvironmentVariable("GS_SELFTEST_REPORT") ?? Path.Combine(Path.GetTempPath(), "gs_selftest.txt");
        var log = new LiveLog(Environment.GetEnvironmentVariable("GS_SELFTEST_REPORT") != null
            ? Path.ChangeExtension(report, ".live.txt")
            : Path.Combine(Path.GetTempPath(), "gs_selftest_live.txt"));

        try
        {
            if (_visualCapture != null)
            {
                await Task.Delay(800);
                await RunVisualSelfTest(log);
                if (App.UnhandledErrorCount != initialErrors) throw new InvalidOperationException("Errore durante il controllo visivo.");
                log.AppendLine("OK");
                File.WriteAllText(report, log.ToString());
                Application.Current.Shutdown();
                return;
            }
            await Task.Delay(1500);
            NewFile();
            await Task.Delay(800);
            await VerifyOverlayScrollGuard(log);
            log.AppendLine($"nuova: battute={_editor.Staff!.Bars.Count} corde={_editor.StringCount} editabile={_editor.IsEditable}");
            await VerifySoundFont(log);
            await VerifyNotePreview(log);
            if (Environment.GetEnvironmentVariable("GS_SELFTEST_PREVIEW_ONLY") == "1")
            {
                if (App.UnhandledErrorCount != initialErrors)
                    throw new InvalidOperationException("Errori non gestiti durante la verifica dell'anteprima.");
                log.AppendLine("OK");
                File.WriteAllText(report, log.ToString());
                _editor.MarkSaved();
                Application.Current.Shutdown();
                return;
            }
            _editor.MarkSaved();
            NewFile();
            await Task.Delay(800);

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

            await VerifyVoiceEdits(log);
            _editor.MarkSaved();
            NewFile();
            await Task.Delay(800);
            await VerifyAudioEdits(log);
            _editor.MarkSaved();
            NewFile();
            await Task.Delay(800);
            await VerifyDynamicsAndGrace(log);
            _editor.MarkSaved();
            NewFile();
            await Task.Delay(800);
            await VerifyChordsAndText(log);
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
            await VerifyAutosave(log);
            if (App.UnhandledErrorCount != initialErrors)
                throw new InvalidOperationException("Errori non gestiti durante l'autotest: controllare crash.log.");
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
        if (_visualCapture != null || Environment.GetEnvironmentVariable("GS_SELFTEST_EXIT") == "1")
        {
            Focus();
            _editor.MarkSaved();
            Application.Current.Shutdown();
        }
    }

    private async Task VerifyNotePreview(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("anteprima OK: " + message);
        }
        async Task Ready()
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while ((!_previewFontReady || !_soundFontReady || !_midiReady || !ReferenceEquals(Api.Score, _editor.Score)) && DateTime.UtcNow < deadline) await Task.Delay(20);
            Require(_previewFontReady && _previewPlayer is { IsReady: true } && _soundFontReady && _midiReady && ReferenceEquals(Api.Score, _editor.Score),
                "sintetizzatore delle anteprime e lettore pronti");
        }
        async Task<GuitarStudio.Audio.PreviewMeasurement> Heard()
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (_previewPlayer!.AudioError is { } error) throw new InvalidOperationException("Uscita anteprima", error);
                if (_previewPlayer.TryGetMeasurement(out var measurement))
                {
                    if (measurement!.Id != _lastPreviewId) continue;
                    Require(measurement.DeviceMs >= measurement.SamplesMs - 1,
                        "AudioClock successivo alla consegna dei campioni");
                    _lastPreviewLatencyMs = measurement!.DeviceMs;
                    log.AppendLine($"anteprima misurata: campioni={measurement.SamplesMs:0.00} ms, dispositivo={measurement.DeviceMs:0.00} ms");
                    Require(measurement.DeviceMs < 50, "anteprima al dispositivo sotto 50 ms");
                    return measurement;
                }
                await Task.Delay(5);
            }
            throw new InvalidOperationException("Nessun campione non silenzioso ha raggiunto la posizione del dispositivo.");
        }
        void Drain() { while (_previewPlayer!.TryGetMeasurement(out _)) { } }
        await Ready(); Drain();
        log.AppendLine($"uscita anteprima: WASAPI condiviso, {GuitarStudio.Audio.PreviewRenderer.SampleRate} Hz, buffer effettivo={_previewPlayer!.BufferMs:0.00} ms");
        var played = _previewPlayed;
        _lastDigit = null; TypeDigit(5);
        Require(!_midiReady && _previewPlayed == played + 1 && _pendingPreview == null,
            "nota inviata al motore separato mentre il MIDI del brano non è pronto");
        _midiLoads.Enqueue((_editor.Score, _revision - 1)); OnMidiLoaded();
        Require(!_midiReady && _previewPlayed == played + 1, "un MidiLoaded vecchio non riavvia la nota");
        await Heard();

        Drain(); played = _previewPlayed;
        _lastDigit = null; TypeDigit(1); TypeDigit(2);
        await Heard();
        Require(_previewPlayed == played + 2 && _lastPreviewFret == 12,
            "due cifre aggiornano immediatamente la stessa anteprima monofonica al tasto 12");
        await Ready(); Drain();

        played = _previewPlayed; _previewFontReady = false;
        _lastDigit = null; TypeDigit(8);
        _editor.CurrentBeat!.RemoveNote(_editor.CurrentNote!);
        _previewFontReady = true; TryPlayPendingPreview();
        Require(_previewPlayed == played && _pendingPreview == null, "nota staccata durante il caricamento scartata");
        _previewFontReady = false; _lastDigit = null; TypeDigit(7); _editor.Undo();
        _previewFontReady = true; TryPlayPendingPreview();
        Require(_previewPlayed == played && _pendingPreview == null, "Annulla scarta la richiesta in attesa dei suoni");

        _lastDigit = null; TypeDigit(9); _editor.MoveRight();
        await Heard();
        Require(_lastPreviewFret == 9 && _editor.CurrentNote == null, "il movimento del cursore conserva la nota appena suonata");
        _previewFontReady = false; TypeDigit(3); _editor.MarkSaved(); NewFile();
        _previewFontReady = true; TryPlayPendingPreview();
        Require(_pendingPreview == null, "Nuova annulla l'anteprima in attesa del vecchio documento");
        await Ready(); Drain();

        _editor.AddTrack("Basso anteprima", 33, new double[] { 43, 38, 33, 28 }, true);
        played = _previewPlayed; _lastDigit = null; TypeDigit(4);
        Require(!_midiReady && _previewPlayed == played + 1, "nuova traccia suona sul canale privato prima del nuovo MIDI");
        await Heard();
        var staff = _editor.Staff!;
        _editor.SetTrackProperties("Basso", 36, staff.Tuning.ToList(), staff.StringTuning.Name, 2);
        _lastDigit = null; TypeDigit(5); await Heard();
        Require(_editor.CurrentNote!.RealValue == _editor.OpenStringPitch(_editor.Line) + 5,
            "anteprima usa l'altezza reale con accordatura e capotasto");

        // Il caricamento passa dalla stessa funzione usata dal menu; dati soltanto nella cartella isolata.
        var custom = Path.Combine(AppPaths.DataDirectory, "suono-personale.sf2");
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.Copy(BundledSoundFont, custom, true);
        LoadSoundFont(custom, false); await Ready();
        Require(AppSettings.Current.SoundFontPath == custom && DefaultSoundFontItem.IsEnabled,
            "SoundFont personale caricato e preferenza salvata");
        _lastDigit = null; TypeDigit(6); await Heard();
        DefaultSoundFont_Click(this, new RoutedEventArgs()); await Ready();
        Require(AppSettings.Current.SoundFontPath == null && !DefaultSoundFontItem.IsEnabled,
            "ripristino del SoundFont incluso anche nel motore delle anteprime");

        _editor.MarkSaved();
        CancelPendingMidiReload();
        _editor.Attach(GuitarStudio.Diagnostics.PerformanceScore.Create(Api.Settings), 0);
        Render(); await Ready();
        _editor.MoveTo(100, 0, 1); Drain();
        var results = new List<GuitarStudio.Audio.PreviewMeasurement>();
        for (var i = 0; i < 12; i++)
        {
            var keyTimer = System.Diagnostics.Stopwatch.StartNew();
            _lastDigit = null; TypeDigit(i % 9 + 1);
            log.AppendLine($"gestore cifra 200×4: {keyTimer.Elapsed.TotalMilliseconds:0.00} ms");
            results.Add(await Heard());
            await Task.Delay(90);
        }
        var samples = results.Select(r => r.SamplesMs).Order().ToArray();
        var devices = results.Select(r => r.DeviceMs).Order().ToArray();
        log.AppendLine($"anteprima 200×4, n={results.Count}: campioni mediana={(samples[5] + samples[6]) / 2:0.00} ms max={samples[^1]:0.00} ms; dispositivo mediana={(devices[5] + devices[6]) / 2:0.00} ms max={devices[^1]:0.00} ms");
        if (Environment.GetEnvironmentVariable("GS_SELFTEST_PREVIEW_REPORT") is { Length: > 0 } previewReport)
            File.WriteAllText(previewReport, System.Text.Json.JsonSerializer.Serialize(results,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Require(devices.All(ms => ms < 50), "tasto → campione raggiunto dal dispositivo sotto 50 ms (12/12)");
        _editor.MarkSaved(); NewFile(); await Ready();
    }

    private async Task VerifyAutosave(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("autosave OK: " + message);
        }

        Require(_autosaveTimer is { IsEnabled: true } && _autosaveTimer.Interval == TimeSpan.FromSeconds(60),
            "timer attivo ogni 60 secondi");
        _autosave ??= new Recovery.AutosaveStore();
        _editor.SetTitle("Verifica recupero automatico");
        var revision = _revision;
        log.AppendLine("autosave: attesa del timer reale (fino a 65 secondi)");
        var deadline = DateTime.UtcNow.AddSeconds(65);
        while ((_autosavedRevision != _revision || !File.Exists(_autosave.FilePath)) && DateTime.UtcNow < deadline)
            await Task.Delay(250);
        log.AppendLine($"autosave: revisione iniziale={revision}, corrente={_revision}, salvata={_autosavedRevision}, modificato={_editor.IsDirty}");
        Require(_autosavedRevision == _revision && File.Exists(_autosave.FilePath), "copia creata dal timer reale");
        var recovered = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(_autosave.FilePath)), Api.Settings);
        Require(recovered.Title == _editor.Score!.Title && recovered.Tracks.Count == _editor.Score.Tracks.Count,
            "copia Guitar Pro leggibile con titolo e tracce attuali");
        Require(_editor.IsDirty, "la copia di recupero non sostituisce Salva");

        var path = Path.Combine(Path.GetTempPath(), $"gs_autosave_selftest_{Guid.NewGuid():N}.gp");
        var recent = AppSettings.Current.RecentFiles.ToList();
        try
        {
            _currentPath = path;
            Require(Save(false), "salvataggio esplicito riuscito");
            Require(!File.Exists(_autosave.FilePath) && !_editor.IsDirty, "Salva elimina la copia di recupero");
            _editor.SetFret(17);
            AutosaveTick(null, EventArgs.Empty);
            Require(File.Exists(_autosave.FilePath), "una nuova modifica ricrea la copia");
            log.AppendLine("autosave chiusura: " + _autosave.FilePath);
            // Closed chiamerà CompleteAutosave: il controllo finale avviene anche dall'esterno.
            _editor.MarkSaved();
        }
        finally
        {
            _currentPath = null;
            AppSettings.Current.RecentFiles = recent;
            AppSettings.Current.Save();
            RebuildRecentMenu();
            File.Delete(path);
        }
    }

    private async Task VerifyVoiceEdits(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            log.AppendLine("voci OK: " + message);
        }

        // Terza battuta del riferimento: melodia lunga sopra un arpeggio indipendente.
        _editor.MoveTo(0, 0, 2);
        _editor.SetFret(3);
        _editor.SetDuration(Duration.Half);
        _editor.MoveTo(0, 3, 2);
        _editor.DeleteBeat();
        _editor.MoveTo(0, 1, 2); _editor.SetFret(0);
        _editor.MoveTo(0, 2, 2); _editor.SetFret(3);
        _editor.SetDuration(Duration.Eighth); _editor.ToggleDot();
        _editor.MoveRight(); _editor.SetDuration(Duration.Sixteenth); _editor.SetFret(2);
        var melody = _editor.CurrentBar!.Voices[0];
        Require(melody.CalculateDuration() == 3840, "la melodia occupa 4/4 (minima, quarto, croma puntata, semicroma)");

        _editor.SelectVoice(1);
        Require(VoiceTwo.IsChecked == true && VoiceOne.IsChecked == false, "il selettore mostra la voce attiva");
        var accompaniment = new[] { (5, 2), (4, 4), (3, 4), (4, 4), (6, 3), (4, 0), (3, 0) };
        _editor.MoveTo(0, 0, 5);
        for (var i = 0; i < accompaniment.Length; i++)
        {
            if (i > 0) _editor.MoveRight();
            _editor.MoveTo(0, _editor.BeatIndex, accompaniment[i].Item1);
            _editor.SetDuration(i == 6 ? Duration.Quarter : Duration.Eighth);
            _editor.SetFret(accompaniment[i].Item2);
        }
        Require(_editor.BarIndex == 0 && _editor.CurrentVoice!.CalculateDuration() == 3840,
            "sette posizioni dell'accompagnamento stanno nella stessa battuta della melodia");
        Require(melody.Beats.Count == 4 && melody.Beats[0].Duration == Duration.Half,
            "scrivere nella voce 2 conserva le durate della voce 1");

        _editor.Copy();
        _editor.Cut();
        Require(_editor.CurrentBar!.Voices[0].Beats.Sum(b => b.Notes.Count) == 4 &&
                _editor.CurrentVoice!.Beats.All(b => b.Notes.Count == 0),
            "tagliare l'accompagnamento conserva la melodia e la battuta");
        _editor.Undo();
        Require(_editor.VoiceIndex == 1 && _editor.CurrentVoice!.Beats.Sum(b => b.Notes.Count) == 7,
            "Annulla ripristina la voce 2");
        _editor.Redo();
        _editor.Paste();
        Require(_editor.Score!.MasterBars.Count == 4 && _editor.CurrentBar!.Voices[0].Beats.Count == 4 &&
                _editor.CurrentVoice!.Beats.Count == 7, "incollare nella voce vuota conserva entrambe le linee senza aggiungere battute");

        var restored = ScoreLoader.LoadScoreFromBytes(new Uint8Array(_editor.ExportGp()), Api.Settings);
        var bar = restored.Tracks[0].Staves[0].Bars[0];
        Require(bar.Voices.Count == 2 && bar.Voices[0].Beats[0].Duration == Duration.Half &&
                bar.Voices[1].Beats[0].Duration == Duration.Eighth,
            "salvataggio e rilettura .gp mantengono due durate sulla stessa posizione");
        var midi = new MidiFile();
        new MidiFileGenerator(restored, Api.Settings, new AlphaSynthMidiFileHandler(midi, true)).Generate();
        var highStart = midi.Events.OfType<NoteOnEvent>().First(n => n.NoteKey == 62).Tick;
        var highEnd = midi.Events.OfType<NoteOffEvent>().First(n => n.NoteKey == 62).Tick;
        var lowStart = midi.Events.OfType<NoteOnEvent>().First(n => n.NoteKey == 47).Tick;
        var lowEnd = midi.Events.OfType<NoteOffEvent>().First(n => n.NoteKey == 47).Tick;
        Require(highStart == lowStart && highEnd - highStart > 3 * (lowEnd - lowStart),
            "il MIDI tiene la melodia mentre l'accompagnamento continua");
        await Task.Delay(400);
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
            async Task WaitForMidi(Func<bool> condition)
            {
                var deadline = DateTime.UtcNow.AddSeconds(4);
                while ((!_midiReady || !condition()) && DateTime.UtcNow < deadline) await Task.Delay(25);
            }
            _editor.MoveTo(0, 0, 6);
            _editor.SetFret(5);
            Require(!_midiReady, "MIDI del brano invalidato dopo la modifica (anteprima indipendente)");
            await WaitForMidi(() => loadedMidi?.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 45) == true);
            Require(loadedMidi?.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 45) == true,
                "la nota scritta nella tablatura vuota arriva al MIDI del lettore");

            _editor.SetStartTempo(75);
            await WaitForMidi(() => Math.Abs(Api.EndTime - 12800) < 2);
            Require(Math.Abs(Api.EndTime - 12800) < 2, "il lettore usa il nuovo BPM (4 battute a 75 BPM = 12,8 s)");

            _editor.SetFret(7);
            await WaitForMidi(() => loadedMidi?.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 47) == true);
            Require(loadedMidi!.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 47) &&
                    !loadedMidi.Events.OfType<NoteOnEvent>().Any(n => n.NoteKey == 45),
                "il lettore sostituisce la vecchia nota dopo una modifica");

            var staff = _editor.Staff!;
            _editor.SetTrackProperties("Chitarra", 25, staff.Tuning.ToList(), staff.StringTuning.Name, 0);
            await WaitForMidi(() => loadedMidi?.Events.OfType<ProgramChangeEvent>().Any(p => p.Program == 25) == true);
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
