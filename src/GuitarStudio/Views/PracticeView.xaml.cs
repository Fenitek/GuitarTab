using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using Path = System.IO.Path;
using AlphaTab.Rendering.Utils;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Model;
using AlphaTab.Synth;
using GuitarStudio.Editing;
using GuitarStudio.Ui;
using Microsoft.Win32;
using Duration = AlphaTab.Model.Duration;

namespace GuitarStudio.Views;

public partial class PracticeView : UserControl
{
    // Esercizio originale: pentatonica di La minore in quinta posizione, su e giù, poi due accordi.
    private const string DemoTex = """
        \title "Esercizio: pentatonica di La minore"
        \subtitle "Posizione 5 - pennata alternata"
        \tempo 90
        .
        :8 5.6 8.6 5.5 7.5 5.4 7.4 5.3 7.3 |
        5.2 8.2 5.1 8.1 8.1 5.1 8.2 5.2 |
        7.3 5.3 7.4 5.4 7.5 5.5 8.6 5.6 |
        :2 (5.6 7.5 7.4) (3.5 5.4 5.3)
        """;

    // Tablatura vuota: 4 battute in 4/4 piene di pause da un quarto.
    private const string NewTex = """
        \title "Nuova tablatura"
        \tempo 120
        .
        \track "Chitarra"
        r.4 r.4 r.4 r.4 | r.4 r.4 r.4 r.4 | r.4 r.4 r.4 r.4 | r.4 r.4 r.4 r.4
        """;

    private bool _initialized;
    private bool _playing;
    private bool _soundFontReady;
    private double _lastTick;

    private TabEditor _editor = null!;
    private string? _currentPath;
    private ScrollViewer? _scrollViewer;
    private bool _expectingLoad;
    private bool _midiReady;

    // Per scrivere tasti a due cifre (es. 1 poi 2 = 12).
    private int? _lastDigit;
    private DateTime _lastDigitTime;
    private (int Bar, int Beat, int Line) _lastDigitPos;

    public PracticeView()
    {
        InitializeComponent();
        ConfigureVisualSelfTest();
        // Modifica l'oggetto prima di OnApplyTemplate, senza invocare il setter Settings:
        // il worker predefinito non deve nascere (la sua dismissione in 1.9 può anche crashare).
        Tab.Settings.Core.UseWorkers = false;
        Loaded += OnLoaded;
    }

    private bool EditMode => PaletteToggle.IsChecked == true;

    /// <summary>API di alphaTab: esiste da quando il controllo ha applicato il template (vedi Setup).</summary>
    private AlphaTabApiBase<AlphaTab.Wpf.AlphaTab> Api => Tab.Api!;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        // Lascia finire al controllo alphaTab la sua inizializzazione prima di configurarlo.
        Dispatcher.BeginInvoke(Setup, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void Setup()
    {
        var settings = new Settings();
        settings.Core.EnableLazyLoading = false;
        // In alphaTab 1.9 il worker può restituire BoundsLookup di una versione precedente
        // contro lo Score già modificato: inserimenti/tagli rapidi causano indici fuori limite.
        // Render sul dispatcher: modello e geometria terminano insieme prima del prossimo edit.
        settings.Core.UseWorkers = false;
        settings.Display.LayoutMode = LayoutMode.Page;
#pragma warning disable CS0618 // StaveProfile è deprecato ma resta il modo più semplice per scegliere tab/spartito
        settings.Display.StaveProfile = StaveProfile.Tab;
#pragma warning restore CS0618
        settings.Display.Scale = 1.0;
        // Mantiene due sistemi interi anche con la linea ritmica, senza ridurre i glifi.
        settings.Display.Padding = new List<double> { 35, 10 };
        settings.Display.SystemPaddingTop = 5;
        settings.Display.SystemPaddingBottom = 5;
        settings.Notation.RhythmMode = TabRhythmMode.ShowWithBars;
        // Nell'editor lo spazio è per le note; titolo e accordatura sono già nelle proprietà.
        settings.Notation.Elements.Set(NotationElement.ScoreTitle, false);
        settings.Notation.Elements.Set(NotationElement.GuitarTuning, false);
        // Entrambe le linee devono essere leggibili sulla carta, anche sullo schermo ad alta risoluzione.
        settings.Display.Resources.SecondaryGlyphColor = settings.Display.Resources.MainGlyphColor;
        settings.Player.PlayerMode = PlayerMode.EnabledSynthesizer;
        settings.Player.EnableCursor = true;
        settings.Player.EnableAnimatedBeatCursor = true;
        settings.Player.EnableSeekToClick = true;
        settings.Player.EnablePlaybackRangeSelection = true;
        settings.Player.ScrollMode = ScrollMode.Continuous;
        Tab.Settings = settings;

        var api = Tab.Api!;
        _editor = new TabEditor(api.Settings);
        _editor.Changed += OnEditorChanged;
        _editor.CursorMoved += OnEditorCursorMoved;
        _editor.TracksChanged += () =>
        {
            RebuildTracksPanel();
        };
        api.MidiLoad.On(_ => Ui(OnMidiLoad));
        api.MidiLoaded.On(_ => Ui(OnMidiLoaded));
        Fretboard.FretClicked += OnFretClicked;
        BuildEffectButtons();
        BuildDynamicsButtons();
        api.PlayedBeatChanged.On(beat => Ui(() => OnPlayedBeat(beat)));

        api.Error.On(err => Ui(() => StatusText.Text = "Errore: " + err.Message));

        api.SoundFontLoaded.On(() => Ui(OnSoundFontLoaded));
        api.Player?.SoundFontLoadFailed.On(_ => Ui(OnSoundFontFailed));

        api.PlayerReady.On(() => Ui(() =>
        {
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = true;
        }));

        api.ScoreLoaded.On(score => Ui(() => OnScoreLoaded(score)));
        api.PostRenderFinished.On(() => Ui(() => UpdateOverlay()));

        api.PlayerStateChanged.On(args => Ui(() =>
        {
            _playing = args.State == PlayerState.Playing;
            if (_playing) { _pendingPreview = null; _previewPlayer?.Stop(); }
            PlayButton.Content = _playing ? "" : "";
        }));

        api.PlayerPositionChanged.On(args => Ui(() =>
        {
            TimeText.Text = $"{Fmt(args.CurrentTime)} / {Fmt(args.EndTime)}";

            // Il loop è ricominciato: la posizione è tornata indietro.
            if (_playing && api.IsLooping && args.CurrentTick + 10 < _lastTick)
                OnLoopRestarted();
            _lastTick = args.CurrentTick;
        }));

        InitLayout();
        RebuildRecentMenu();
        LoadSoundFont(AppSettings.Current.SoundFontPath, announce: false);

        StartAutosave();
        if (TryRecoverAutosave())
        {
            if (App.StartupFile is { } requested)
                ShowToast($"Tablatura recuperata. Il file richiesto «{Path.GetFileName(requested)}» non è stato aperto.", ToastKind.Warning);
            return;
        }

        // Un file passato sulla riga di comando ha la precedenza sull'esercizio demo.
        if (App.StartupFile is { } startup && OpenPath(startup, confirm: false)) return;
        api.Tex(DemoTex, null);
    }

    private ScrollViewer? ScrollView
    {
        get
        {
            if (_scrollViewer == null && Tab.Template?.FindName("PART_ScrollView", Tab) is ScrollViewer sv)
            {
                _scrollViewer = sv;
                sv.ScrollChanged += (_, _) => UpdateOverlay(ensureVisible: false);
            }
            return _scrollViewer;
        }
    }

    private void OnScoreLoaded(Score score)
    {
        // alphaTab segnala ScoreLoaded anche a ogni RenderScore, in modo asincrono: dopo un
        // annulla rapido potrebbe arrivare una versione vecchia. Si accetta una partitura nuova
        // solo se è stata aperta/creata esplicitamente.
        if (score != _editor.Score && (_expectingLoad || _editor.Score == null))
        {
            _expectingLoad = false;
            var track = Api.Tracks.Count > 0 ? (int)Api.Tracks[0].Index : 0;
            _editor.Attach(score, track);
            RebuildTracksPanel();
        }
        UpdateTitle();
        UpdateEditUi();
        MaybeRunSelfTest();
        MaybeRunPerformanceBenchmark();
    }

    // ================================================================ file

    private void Open_Click(object sender, RoutedEventArgs e) => OpenFile();

    private void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog
        {
            Title = "Apri una tablatura",
            Filter = "Tablature (*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.xml;*.musicxml;*.mxl;*.capx)|*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.xml;*.musicxml;*.mxl;*.capx|Tutti i file|*.*"
        };
        if (dlg.ShowDialog() == true) OpenPath(dlg.FileName, confirm: false);
    }

    /// <summary>Apre una tablatura dal disco. Restituisce false se non ci riesce o se l'utente annulla.</summary>
    public bool OpenPath(string path, bool confirm = true)
    {
        if (_editor == null) return false; // il motore non è ancora pronto
        if (confirm && !ConfirmDiscard()) return false;
        try
        {
            // Si legge prima il file: se non è valido, la tablatura aperta resta al suo posto.
            var score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(path)), Api.Settings);
            Api.Stop();
            CancelPendingMidiReload();
            ClearAutosave();
            _expectingLoad = true;
            _currentPath = path;
            Api.RenderScore(score, new List<double> { 0 }, null);
            AppSettings.Current.AddRecent(path);
            RebuildRecentMenu();
            StatusText.Text = "Aperto: " + Path.GetFileName(path);
            return true;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Dialogs.Warning(Window.GetWindow(this),
                $"Non riesco ad aprire «{Path.GetFileName(path)}».\n\n" +
                "Il formato non è tra quelli supportati (Guitar Pro 3–8, MusicXML, Capella) oppure il file è danneggiato.");
            return false;
        }
    }

    private void New_Click(object sender, RoutedEventArgs e) => NewFile();

    private void NewFile()
    {
        if (!ConfirmDiscard()) return;
        ClearAutosave();
        Api.Stop();
        CancelPendingMidiReload();
        _currentPath = null;
        _expectingLoad = true;
        Api.Tex(NewTex, null);
        SetEditMode(true);
        StatusText.Text = "Nuova tablatura: scrivi i numeri dei tasti, muoviti con le frecce.";
    }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        ClearAutosave();
        Api.Stop();
        CancelPendingMidiReload();
        _currentPath = null;
        _expectingLoad = true;
        Api.Tex(DemoTex, null);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save(false);
    private void SaveAs_Click(object sender, RoutedEventArgs e) => Save(true);

    private bool Save(bool askPath)
    {
        if (_editor.Score == null) return false;
        var path = _currentPath;
        var ext = path != null ? Path.GetExtension(path).ToLowerInvariant() : "";

        // I formati vecchi (.gp5, .gpx…) si possono leggere ma non scrivere: si salva in .gp.
        if (askPath || path == null || (ext != ".gp" && ext != ".atex"))
        {
            var dlg = new SaveFileDialog
            {
                Title = "Salva tablatura",
                FileName = SafeFileName(_editor.Score.Title),
                Filter = "Guitar Pro 7/8 (*.gp)|*.gp|alphaTex, testo (*.atex)|*.atex",
                InitialDirectory = path != null ? Path.GetDirectoryName(path) : null
            };
            if (dlg.ShowDialog() != true) return false;
            path = dlg.FileName;
            ext = Path.GetExtension(path).ToLowerInvariant();
        }

        try
        {
            if (ext == ".atex")
                File.WriteAllText(path, _editor.ExportAlphaTex());
            else
                File.WriteAllBytes(path, _editor.ExportGp());
            _currentPath = path;
            _editor.MarkSaved();
            UpdateTitle();
            UpdateLcd();
            AppSettings.Current.AddRecent(path);
            RebuildRecentMenu();
            ShowToast("Salvato: " + Path.GetFileName(path), ToastKind.Ok);
            ClearAutosave();
            return true;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Dialogs.Warning(Window.GetWindow(this), "Salvataggio non riuscito:\n" + ex.Message);
            return false;
        }
    }

    /// <summary>Chiede se salvare le modifiche. Restituisce false se l'utente annulla.</summary>
    public bool ConfirmDiscard()
    {
        if (_editor == null || !_editor.IsDirty) return true;
        var title = string.IsNullOrWhiteSpace(_editor.Score?.Title) ? "la tablatura" : $"«{_editor.Score!.Title}»";
        var r = Dialogs.SaveChanges(Window.GetWindow(this), $"Vuoi salvare le modifiche a {title}?");
        return r switch
        {
            MessageBoxResult.Yes => Save(false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    // ================================================================ vista e tracce

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tab?.Api == null || !_initialized) return;
#pragma warning disable CS0618 // vedi Setup
        Api.Settings.Display.StaveProfile = ViewCombo.SelectedIndex switch
        {
            1 => StaveProfile.Tab,
            2 => StaveProfile.Score,
            _ => StaveProfile.ScoreTab
        };
#pragma warning restore CS0618
        // Il pentagramma ha già le durate: evita una seconda linea ritmica nella tab.
        Api.Settings.Notation.RhythmMode = ViewCombo.SelectedIndex == 1
            ? TabRhythmMode.ShowWithBars : TabRhythmMode.Hidden;
        Api.UpdateSettings();
        Api.Render();
    }

    private void Render()
    {
        CancelPendingEditorRender();
        if (_editor.Score == null) return;
        Api.RenderScore(_editor.Score, new List<double> { _editor.TrackIndex }, null);
    }

    // ================================================================ editor

    private void OnEditorChanged()
    {
        var total = Stopwatch.StartNew();
        _pendingPreview = null;
        _previewPlayer?.Stop();
        _lastDeferredRenderMs = 0;
        _lastDeferredMidiMs = 0;
        _revision++;
        // RenderScore rigenera il MIDI solo quando cambia l'istanza di Score (es. Annulla).
        // Scrivere note o cambiare BPM/suono modifica invece lo stesso oggetto: senza questo
        // aggiornamento il lettore continua a suonare il brano precedente, anche solo pause.
        var reloadMidi = ReferenceEquals(Api.Score, _editor.Score);
        Api.Stop();
        // Mai suonare anteprime con i canali del vecchio MIDI, soprattutto dopo nuove tracce.
        ScheduleMidiReload();
        var step = Stopwatch.StartNew();
        if (reloadMidi) ScheduleEditorRender();
        else Render();
        var renderMs = step.Elapsed.TotalMilliseconds;
        step.Restart();
        // Una nuova istanza di Score avvia già un caricamento interno ad alphaTab.
        // Per gli edit nello stesso modello raggruppa le cifre prima di rigenerare il MIDI.
        if (!reloadMidi) _midiReloadTimer?.Stop();
        var midiMs = step.Elapsed.TotalMilliseconds;
        UpdateTitle();
        UpdateEditUi();
        step.Restart();
        RebuildTracksPanel();
        _lastChangeTiming = new EditorChangeTiming(total.Elapsed.TotalMilliseconds, renderMs, midiMs, step.Elapsed.TotalMilliseconds);
    }

    private void OnEditorCursorMoved()
    {
        UpdateEditUi();
        UpdateOverlay();

        // Come in Guitar Pro: il punto di partenza della riproduzione segue il cursore.
        if (!_playing && _editor.CurrentBeat is { } beat && Api.IsReadyForPlayback)
            Api.TickPosition = beat.AbsolutePlaybackStart;
    }

    private void UpdateEditUi()
    {
        if (_editor?.Score == null) return;
        UndoButton.IsEnabled = _editor.CanUndo;
        RedoButton.IsEnabled = _editor.CanRedo;
        PasteButton.IsEnabled = _editor.HasClipboard;

        var beat = _editor.CurrentBeat;
        VoiceOne.IsChecked = _editor.VoiceIndex == 0;
        VoiceTwo.IsChecked = _editor.VoiceIndex == 1;
        VoiceOne.IsEnabled = VoiceTwo.IsEnabled = _editor.IsEditable;
        var duration = beat?.Duration ?? _editor.CurrentDuration;
        foreach (var t in new[] { DurWhole, DurHalf, DurQuarter, DurEighth, DurSixteenth, DurThirtySecond })
            t.IsChecked = int.Parse((string)t.Tag) == (int)duration;
        DotToggle.IsChecked = beat?.Dots > 0;
        TupletToggle.IsChecked = beat?.HasTuplet == true;

        var note = _editor.CurrentNote;
        foreach (var t in _effectToggles)
            t.IsChecked = TabEditor.HasEffect(beat, note, (NoteEffect)t.Tag);
        foreach (var t in _dynamicToggles)
            t.IsChecked = beat != null && beat.Dynamics == (DynamicValue)t.Tag;
        GraceBeforeToggle.IsChecked = beat?.GraceType == GraceType.BeforeBeat;
        GraceOnToggle.IsChecked = beat?.GraceType == GraceType.OnBeat;

        UpdateFretboard();
        UpdateLcd();
        UpdateInspector();
        RefreshTrackHighlight();

        if (!EditMode || !_editor.IsEditable)
        {
            CursorText.Text = "";
            return;
        }

        var staff = _editor.Staff!;
        var stringName = Tuning.GetTextForTuning(Note.GetStringTuning(staff, _editor.ModelString(_editor.Line)), false);
        var fret = note == null ? "—" : note.IsDead ? "X" : note.Fret.ToString("0");
        var text = $"Battuta {_editor.BarIndex + 1}/{staff.Bars.Count} · Voce {_editor.VoiceIndex + 1} · Tempo {_editor.BeatIndex + 1} · Corda {_editor.Line} ({stringName}) · Tasto {fret}";
        if (_editor.CurrentBeat is { } current)
        {
            text += $" · {TabEditor.DynamicLabel(current.Dynamics)}";
            if (current.Chord is { } chordName) text += $" · accordo {chordName.Name}";
            if (current.GraceType != GraceType.None) text += $" · abbellimento {TabEditor.GraceLabel(current.GraceType)}";
        }
        if (_editor.HasSelection) text = $"{_editor.SelectedBeats().Count} tempi selezionati  ·  " + text;

        if (_editor.BarFill(out var used, out var total) && Math.Abs(used - total) > 0.5)
        {
            var diff = Math.Abs(total - used) / 960.0; // 960 tick = un movimento da 1/4
            text += used < total
                ? $"  ·  ⚠ mancano {diff:0.##} mov."
                : $"  ·  ⚠ {diff:0.##} mov. in più";
        }
        CursorText.Text = text;
    }

    // Manico --------------------------------------------------------

    private void UpdateFretboard(Beat? playing = null)
    {
        if (FretPanel.Visibility != Visibility.Visible || _editor?.Staff is not { } staff || !_editor.IsEditable) return;
        var n = _editor.StringCount;
        var names = Enumerable.Range(1, n)
            .Select(l => Tuning.GetTextForTuning(Note.GetStringTuning(staff, _editor.ModelString(l)), false))
            .ToList();
        var beat = playing ?? _editor.CurrentBeat;
        var notes = beat == null
            ? new List<(int, int)>()
            : beat.Notes.Where(x => !x.IsDead).Select(x => (_editor.LineOf((int)x.String), (int)x.Fret)).ToList();
        Fretboard.Show(n, names, (int)staff.Capo, _editor.Line, notes, playing != null);
        ApplyPanelHeights();
    }

    private void OnPlayedBeat(Beat beat)
    {
        if (!_playing || beat.Voice?.Bar?.Staff != _editor.Staff) return;
        UpdateFretboard(beat);
    }

    private void OnFretClicked(int line, int fret)
    {
        if (!_editor.IsEditable) return;
        if (!EditMode) SetEditMode(true);
        _editor.ToggleFretAt(line, fret);
        PreviewCurrentNote();
        Focus();
    }

    // Cursore e selezione sopra la tab -------------------------------

    private readonly List<Rectangle> _selectionRects = new();
    private bool _overlayScrollPending;
    private int _overlayAutoScrollCount;

    /// <summary>Disegna cursore (colonna + casella della corda) e selezione sopra la tab.</summary>
    private void UpdateOverlay(bool ensureVisible = true)
    {
        ColumnMark.Visibility = CellMark.Visibility = Visibility.Collapsed;
        foreach (var r in _selectionRects) r.Visibility = Visibility.Collapsed;
        if (!EditMode || _editor?.IsEditable != true || _editor.CurrentBeat is not { } beat) return;

        var lookup = Api.BoundsLookup;
        var sv = ScrollView;
        if (lookup == null || sv?.Content is not FrameworkElement content) return;

        var lines = Math.Max(2, _editor.StringCount);

        // Selezione: un rettangolo per ogni tempo selezionato.
        if (_editor.HasSelection)
        {
            var i = 0;
            foreach (var sel in _editor.SelectedBeats())
            {
                var tb = TabBarBounds(lookup, (int)sel.Voice.Bar.Index);
                var sb = tb?.Beats.FirstOrDefault(x => x.Beat == sel);
                if (tb == null || sb == null) continue;
                var sp = tb.VisualBounds.H / (lines - 1);
                if (i == _selectionRects.Count)
                {
                    var rect = new Rectangle { Fill = (Brush)FindResource("EditorRangeSelection") };
                    _selectionRects.Add(rect);
                    SelectionLayer.Children.Add(rect);
                }
                var r = _selectionRects[i++];
                var p = content.TranslatePoint(new Point(sb.RealBounds.X, tb.VisualBounds.Y - sp * 0.6), Overlay);
                Canvas.SetLeft(r, p.X);
                Canvas.SetTop(r, p.Y);
                r.Width = sb.RealBounds.W;
                r.Height = tb.VisualBounds.H + sp * 1.2;
                r.Visibility = Visibility.Visible;
            }
        }

        var tabBar = TabBarBounds(lookup, _editor.BarIndex);
        var bb = tabBar?.Beats.FirstOrDefault(x => x.Beat == beat);
        if (tabBar == null || bb == null) return;

        var area = tabBar.VisualBounds;
        var spacing = area.H / (lines - 1);
        var w = Math.Max(22, bb.VisualBounds.W + 10);
        var x = bb.OnNotesX - w / 2;
        var y = area.Y + (_editor.Line - 1) * spacing;

        var colTop = content.TranslatePoint(new Point(x, area.Y - spacing * 0.6), Overlay);
        Canvas.SetLeft(ColumnMark, colTop.X);
        Canvas.SetTop(ColumnMark, colTop.Y);
        ColumnMark.Width = w;
        ColumnMark.Height = area.H + spacing * 1.2;

        var cell = content.TranslatePoint(new Point(x, y - spacing * 0.55), Overlay);
        Canvas.SetLeft(CellMark, cell.X);
        Canvas.SetTop(CellMark, cell.Y);
        CellMark.Width = w;
        CellMark.Height = spacing * 1.1;

        ColumnMark.Visibility = CellMark.Visibility = Visibility.Visible;

        // Tiene il cursore dentro la parte visibile.
        var inView = content.TranslatePoint(new Point(x, y), sv);
        // ScrollChanged richiama UpdateOverlay: una sola correzione per giro del dispatcher,
        // nessuna correzione se non esiste una zona interna stabile, né per frazioni di pixel.
        if (ensureVisible && !_overlayScrollPending && sv.ViewportHeight >= 90 && (inView.Y < 30 || inView.Y > sv.ViewportHeight - 30))
        {
            var offset = Math.Clamp(sv.VerticalOffset + inView.Y - sv.ViewportHeight / 3, 0, sv.ScrollableHeight);
            if (Math.Abs(offset - sv.VerticalOffset) > 1)
            {
                _overlayScrollPending = true;
                _overlayAutoScrollCount++;
                sv.ScrollToVerticalOffset(offset);
                Dispatcher.BeginInvoke(() => _overlayScrollPending = false,
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }
    }

    /// <summary>Riquadro del pentagramma della tab della battuta (l'ultimo disegnato per lo staff).</summary>
    private BarBounds? TabBarBounds(BoundsLookup lookup, int barIndex)
    {
        var mbb = lookup.FindMasterBarByIndex(barIndex);
        return mbb?.Bars.LastOrDefault(b => b.Bar.Staff == _editor.Staff) ?? mbb?.Bars.LastOrDefault();
    }

    private void Tab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!EditMode || _editor?.IsEditable != true) return;
        var lookup = Api.BoundsLookup;
        if (lookup == null || ScrollView?.Content is not FrameworkElement content) return;

        var p = e.GetPosition(content);
        foreach (var sys in lookup.StaffSystems)
        {
            var sb = sys.RealBounds;
            if (p.Y < sb.Y || p.Y > sb.Y + sb.H) continue;
            foreach (var mbb in sys.Bars)
            {
                var mb = mbb.RealBounds;
                if (p.X < mb.X || p.X > mb.X + mb.W) continue;

                var tabBar = mbb.Bars.LastOrDefault(b => b.Bar.Staff == _editor.Staff) ?? mbb.Bars.LastOrDefault();
                if (tabBar == null) return;
                var beat = tabBar.Beats
                    .Where(b => b.Beat.Voice.Index == _editor.VoiceIndex)
                    .OrderBy(b => Math.Abs(b.OnNotesX - p.X))
                    .FirstOrDefault();
                if (beat == null) return;

                var line = _editor.Line;
                var area = tabBar.VisualBounds;
                var spacing = area.H / Math.Max(1, _editor.StringCount - 1);
                if (p.Y >= area.Y - spacing && p.Y <= area.Y + area.H + spacing)
                    line = (int)Math.Round((p.Y - area.Y) / spacing) + 1;

                // Maiusc+clic estende la selezione, come in Guitar Pro.
                var extend = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                _editor.MoveTo((int)mbb.Index, (int)beat.Beat.Index, line, extend);
                Dispatcher.BeginInvoke(() => Focus());
                return;
            }
        }
    }

    // Tastiera -------------------------------------------------------

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_editor == null || e.OriginalSource is TextBox or ComboBox or ComboBoxItem or Slider or MenuItem || e.Key == Key.F1) return;
        if (TryHandleToolbarShortcut(e)) return;
        if (e.OriginalSource is GridSplitter) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = CurrentModifiers.HasFlag(ModifierKeys.Control);
        var shift = CurrentModifiers.HasFlag(ModifierKeys.Shift);
        e.Handled = true;

        // Scorciatoie sempre attive.
        if (ctrl)
        {
            switch (key)
            {
                case Key.O: OpenFile(); return;
                case Key.N: NewFile(); return;
                case Key.S: Save(shift); return;
            }
        }
        if (key == Key.Space) { if (shift) Stop_Click(this, new RoutedEventArgs()); else Play_Click(this, new RoutedEventArgs()); return; }

        if (!EditMode || !_editor.IsEditable)
        {
            e.Handled = false;
            return;
        }

        if (ctrl && CurrentModifiers.HasFlag(ModifierKeys.Alt) && key is Key.D1 or Key.NumPad1 or Key.D2 or Key.NumPad2)
        {
            _editor.SelectVoice(key is Key.D1 or Key.NumPad1 ? 0 : 1);
            _lastDigit = null;
            return;
        }

        if (ctrl)
        {
            switch (key)
            {
                case Key.Z: _editor.Undo(); return;
                case Key.Y: _editor.Redo(); return;
                case Key.C: Copy_Click(this, e); return;
                case Key.X: _editor.Cut(); return;
                case Key.V: _editor.Paste(); return;
                case Key.A: _editor.SelectAll(); return;
                case Key.B: ShowInspector(TabBar); return;
                case Key.T: ShowInspector(TabTrack); return;
                case Key.F: _editor.FixBar(); return;
                case Key.D3 or Key.NumPad3: _editor.ToggleTuplet(); return;
                case Key.G: ApplyGrace(shift ? GraceType.OnBeat : GraceType.BeforeBeat); return;
            }
        }

        var digit = DigitOf(key);
        if (digit >= 0 && !ctrl)
        {
            TypeDigit(digit);
            return;
        }

        switch (key)
        {
            case Key.Escape: _editor.ClearSelection(); break;
            case Key.Left when ctrl: _editor.PreviousBar(shift); break;
            case Key.Right when ctrl: _editor.NextBar(shift); break;
            case Key.Left: _editor.MoveLeft(shift); break;
            case Key.Right: _editor.MoveRight(shift); break;
            case Key.Up: _editor.MoveUp(); break;
            case Key.Down: _editor.MoveDown(); break;
            case Key.Home: _editor.FirstBeat(shift); break;
            case Key.End: _editor.LastBeat(shift); break;

            case Key.Delete when ctrl && shift: _editor.DeleteBar(); break;
            case Key.Delete when ctrl: _editor.DeleteBeat(); break;
            case Key.Delete:
            case Key.Back: _editor.DeleteAtCursor(); break;
            case Key.Insert when ctrl: _editor.InsertBarAfter(); break;
            case Key.Insert: _editor.InsertBeat(); break;

            case Key.Add:
            case Key.OemPlus: _editor.LongerDuration(); break;
            case Key.Subtract:
            case Key.OemMinus: _editor.ShorterDuration(); break;
            case Key.OemPeriod:
            case Key.Decimal: _editor.ToggleDot(); break;
            case Key.R: _editor.MakeRest(); break;
            case Key.D: StepDynamics(shift ? -1 : +1); break;

            case Key.H: ApplyEffect(NoteEffect.HammerPull); break;
            case Key.S: ApplyEffect(NoteEffect.Slide); break;
            case Key.B: ApplyEffect(NoteEffect.Bend); break;
            case Key.V: ApplyEffect(NoteEffect.Vibrato); break;
            case Key.P: ApplyEffect(NoteEffect.PalmMute); break;
            case Key.X: ApplyEffect(NoteEffect.Dead); break;
            case Key.L: ApplyEffect(NoteEffect.LetRing); break;
            case Key.T when shift: EditBeatText(); break;
            case Key.T: ApplyEffect(NoteEffect.Tie); break;
            case Key.C: EditChord(); break;
            case Key.G: ApplyEffect(NoteEffect.Ghost); break;
            case Key.A: ApplyEffect(NoteEffect.Accent); break;
            case Key.N: ApplyEffect(NoteEffect.HarmonicNatural); break;

            default: e.Handled = false; break;
        }
    }

    private static int DigitOf(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => -1
    };

    private void TypeDigit(int digit)
    {
        var requestedAt = Stopwatch.GetTimestamp();
        var pos = (_editor.BarIndex, _editor.BeatIndex, _editor.Line);
        var now = DateTime.Now;
        var fret = digit;

        if (_lastDigit is > 0 && pos == _lastDigitPos && (now - _lastDigitTime).TotalMilliseconds < 900
            && _lastDigit.Value * 10 + digit <= 24)
        {
            fret = _lastDigit.Value * 10 + digit;
            _lastDigit = null;
        }
        else
        {
            _lastDigit = digit;
        }
        _lastDigitTime = now;
        _lastDigitPos = pos;

        _editor.SetFret(fret);
        PreviewCurrentNote(requestedAt);
    }

    /// <summary>Fa sentire la nota appena scritta.</summary>
    private void PreviewCurrentNote(long? requestedAt = null)
    {
        _previewRequested++;
        _pendingPreview = null;
        if (_playing || _editor.Score is not { } score || _editor.CurrentNote is not { } note) return;
        _pendingPreview = new PendingPreview(score, note, _revision, requestedAt ?? Stopwatch.GetTimestamp());
        TryPlayPendingPreview();
    }

    private void ApplyEffect(NoteEffect effect)
    {
        // Hammer-on, slide e legatura collegano due note: se l'altra manca l'effetto verrebbe
        // scartato senza dire niente, quindi lo spieghiamo invece di applicarlo.
        if (_editor.LinkWarning(effect) is { } warning)
        {
            ShowToast(warning, ToastKind.Warning);
            return;
        }
        if (!_editor.ToggleEffect(effect))
            ShowToast("Qui non c'è una nota a cui applicare l'effetto.\nScrivi prima il numero del tasto (0–9), poi aggiungi l'effetto.",
                ToastKind.Warning);
    }

    // Pulsanti della barra dell'editor --------------------------------

    private void Duration_Click(object sender, RoutedEventArgs e)
    {
        var d = (Duration)int.Parse((string)((FrameworkElement)sender).Tag);
        _editor.SetDuration(d);
        UpdateEditUi();
        Focus();
    }

    private void Voice_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null) return;
        _editor.SelectVoice(int.Parse((string)((FrameworkElement)sender).Tag));
        _lastDigit = null;
        Focus();
    }

    private void Dot_Click(object sender, RoutedEventArgs e) { _editor.ToggleDot(); UpdateEditUi(); Focus(); }
    private void Tuplet_Click(object sender, RoutedEventArgs e) { _editor.ToggleTuplet(); UpdateEditUi(); Focus(); }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var n = _editor.Copy();
        if (n > 0) ShowToast(n == 1 ? "Copiato 1 tempo." : $"Copiati {n} tempi.", ToastKind.Ok);
        else ShowToast("Niente da copiare.");
        UpdateEditUi();
        Focus();
    }

    private void Cut_Click(object sender, RoutedEventArgs e) { _editor.Cut(); Focus(); }
    private void Paste_Click(object sender, RoutedEventArgs e) { _editor.Paste(); Focus(); }
    private void Undo_Click(object sender, RoutedEventArgs e) { _editor.Undo(); Focus(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { _editor.Redo(); Focus(); }
    private void InsertBeat_Click(object sender, RoutedEventArgs e) { _editor.InsertBeat(); Focus(); }
    private void DeleteBeat_Click(object sender, RoutedEventArgs e) { _editor.DeleteBeat(); Focus(); }
    private void InsertBar_Click(object sender, RoutedEventArgs e) { _editor.InsertBarAfter(); Focus(); }
    private void DeleteBar_Click(object sender, RoutedEventArgs e) { _editor.DeleteBar(); Focus(); }
    private void FixBar_Click(object sender, RoutedEventArgs e) { _editor.FixBar(); Focus(); }

    private void AddTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Score == null) return;
        var dlg = new TrackPropertiesWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Result is { } r)
        {
            _editor.AddTrack(r.Name, r.Program, r.Tuning, r.IsBass);
            if (r.Capo > 0) _editor.SetTrackProperties(r.Name, r.Program, r.Tuning, r.TuningName, r.Capo);
            ShowToast($"Aggiunta la traccia «{r.Name}».", ToastKind.Ok);
        }
        Focus();
    }

    private void DeleteTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Score is not { } score || score.Tracks.Count <= 1)
        {
            ShowToast("Una canzone deve avere almeno una traccia.", ToastKind.Warning);
            return;
        }
        var name = _editor.Track?.Name;
        if (!Dialogs.Confirm(Window.GetWindow(this), $"Eliminare la traccia «{name}»?\nPuoi tornare indietro con Annulla (Ctrl+Z).",
                "Elimina traccia", "Elimina")) return;
        _editor.DeleteTrack();
        Focus();
    }

    // ================================================================ trasporto

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (!_soundFontReady || !_midiReady || !Api.IsReadyForPlayback)
        {
            ShowToast(!_soundFontReady
                ? "I suoni non sono ancora pronti. Se il problema continua, ricarica il SoundFont dal menu ☰."
                : "Preparazione del brano in corso: riprova tra un momento.", ToastKind.Warning);
            return;
        }
        // Il ricaricamento MIDI ripristina i volumi dei canali: conserva le scelte del mixer.
        if (!_playing && _editor.Score is { } score)
        {
            foreach (var track in score.Tracks)
            {
                var tracks = new List<AlphaTab.Model.Track> { track };
                Api.ChangeTrackMute(tracks, track.PlaybackInfo.IsMute);
                Api.ChangeTrackSolo(tracks, track.PlaybackInfo.IsSolo);
                Api.ChangeTrackVolume(tracks, _trackVolumes.GetValueOrDefault((int)track.Index, 1));
            }
        }
        if (TrainerToggle.IsChecked == true && !_playing && Api.TickPosition == 0)
            SetSpeed(ReadInt(TrainerStart, 60));
        Api.PlayPause();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => Api.Stop();

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedText == null) return;
        SpeedText.Text = $"{(int)e.NewValue}%";
        if (_initialized && Tab?.Api != null) Api.PlaybackSpeed = e.NewValue / 100.0;
    }

    private void SetSpeed(int percent)
    {
        SpeedSlider.Value = Math.Clamp(percent, (int)SpeedSlider.Minimum, (int)SpeedSlider.Maximum);
    }

    private void LoopToggle_Click(object sender, RoutedEventArgs e) => Api.IsLooping = LoopToggle.IsChecked == true;

    private void MetronomeToggle_Click(object sender, RoutedEventArgs e) =>
        Api.MetronomeVolume = MetronomeToggle.IsChecked == true ? 1 : 0;

    private void CountInToggle_Click(object sender, RoutedEventArgs e) =>
        Api.CountInVolume = CountInToggle.IsChecked == true ? 1 : 0;

    // ================================================================ speed trainer

    private void OnLoopRestarted()
    {
        if (TrainerToggle.IsChecked != true) return;
        var target = ReadInt(TrainerTarget, 100);
        var step = Math.Max(1, ReadInt(TrainerStep, 5));
        var current = (int)SpeedSlider.Value;
        if (current >= target)
        {
            ShowToast($"Obiettivo raggiunto: {target}%", ToastKind.Ok);
            return;
        }
        var next = Math.Min(target, current + step);
        SetSpeed(next);
        StatusText.Text = $"Giro completato, velocità → {next}%";
    }

    // ================================================================ utilità

    private static int ReadInt(TextBox box, int fallback) =>
        int.TryParse(box.Text.Trim(), out var v) ? v : fallback;

    private static string Fmt(double ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    private void Ui(Action a)
    {
        if (Dispatcher.CheckAccess()) a();
        else Dispatcher.BeginInvoke(a);
    }
}
