using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GuitarStudio.Ui;
using Microsoft.Win32;
using Path = System.IO.Path;
using GuitarStudio.Audio;
using GuitarStudio.Diagnostics;

namespace GuitarStudio.Views;

/// <summary>
/// Contorno della sezione: avvisi a comparsa, guida, file recenti, SoundFont, esportazione
/// e larghezza dei pannelli laterali.
/// </summary>
public partial class PracticeView
{
    // ================================================================ avvisi a comparsa

    private enum ToastKind { Info, Ok, Warning }

    private DispatcherTimer? _toastTimer;

    /// <summary>Mostra un avviso ben visibile sopra lo spartito (e lo ripete nella barra di stato).</summary>
    private void ShowToast(string message, ToastKind kind = ToastKind.Info)
    {
        StatusText.Text = message.Replace('\n', ' ');
        ToastText.Text = message;
        ToastIcon.Text = kind switch
        {
            ToastKind.Ok => "",
            ToastKind.Warning => "",
            _ => ""
        };
        Toast.Visibility = Visibility.Visible;
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));

        if (_toastTimer == null)
        {
            _toastTimer = new DispatcherTimer();
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(380));
                fade.Completed += (_, _) =>
                {
                    if (!_toastTimer.IsEnabled) Toast.Visibility = Visibility.Collapsed;
                };
                Toast.BeginAnimation(OpacityProperty, fade);
            };
        }
        // Più il messaggio è lungo, più resta a schermo.
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(2.2 + message.Length * 0.05, 3, 10));
        _toastTimer.Start();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        (Window.GetWindow(this) as MainWindow)?.ShowHelp();
    }

    // ================================================================ file recenti

    // Un solo elenco per tablature e fogli di accordi: OpenPath manda i fogli alla sezione Accordi.
    private void RebuildRecentMenu() =>
        RecentFilesMenu.Fill(RecentMenu, path => OpenPath(path), message => ShowToast(message, ToastKind.Warning));

    // ================================================================ foglio di accordi

    private void NewChordSheet_Click(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NewChordSheet();

    /// <summary>«Nuova tablatura» dal menu File del foglio di accordi.</summary>
    public void NewTablature() => NewFile();

    /// <summary>Titolo della finestra quando la sezione torna visibile.</summary>
    public void RefreshWindowTitle() => UpdateTitle();

    /// <summary>Autotest del foglio di accordi, fornito da MainWindow: (riga del resoconto, fotografo, profilo 200%).</summary>
    internal Func<Action<string>, VisualCapture?, bool, Task>? ChordSheetSelfTest;

    // ================================================================ SoundFont

    private const string BundledSoundFontName = "GeneralUser GS 1.471 (incluso)";
    private static string BundledSoundFont => Path.Combine(AppContext.BaseDirectory, "Assets", "GeneralUser-GS.sf2");
    private string _soundFontName = BundledSoundFontName;
    private int _soundFontLoadGeneration;
    private readonly Queue<int> _soundFontLoads = new();

    /// <summary>Carica il SoundFont scelto dall'utente, oppure quello incluso se il percorso è null.</summary>
    private async void LoadSoundFont(string? customPath, bool announce)
    {
        var generation = ++_soundFontLoadGeneration;
        SoundStartupProbe.LoadStarted();
        _previewFontReady = false;
        _previewPlayer?.Stop();
        _soundFontReady = false;
        var settings = AppSettings.Current;
        if (customPath != null && !File.Exists(customPath))
        {
            ShowToast($"Il SoundFont «{Path.GetFileName(customPath)}» non si trova più: uso quello incluso.", ToastKind.Warning);
            customPath = null;
            settings.SoundFontPath = null;
            settings.Save();
        }

        var path = customPath ?? BundledSoundFont;
        if (!File.Exists(path))
        {
            StatusText.Text = "SoundFont non trovato: la riproduzione non funzionerà.";
            return;
        }

        try
        {
            StatusText.Text = "Caricamento dei suoni…";
            var bytes = await Task.Run(() => File.ReadAllBytes(path));
            if (generation != _soundFontLoadGeneration || _previewDisposed) return;
            if (!LooksLikeSoundFont(bytes))
            {
                Dialogs.Warning(Window.GetWindow(this), $"«{Path.GetFileName(path)}» non è un SoundFont valido (.sf2 o .sf3).");
                if (customPath != null) LoadSoundFont(null, false);
                return;
            }

            if (_playing) Api.Stop();
            _soundFontReady = false;
            _soundFontName = customPath != null ? Path.GetFileName(path) : BundledSoundFontName;
            _announceSoundFont = announce;
            _soundFontLoads.Enqueue(generation);
            Api.LoadSoundFont(bytes, false);

            if (settings.SoundFontPath != customPath)
            {
                settings.SoundFontPath = customPath;
                settings.Save();
            }
            DefaultSoundFontItem.IsEnabled = customPath != null;
            try
            {
                var renderer = await Task.Run(() => new PreviewRenderer(bytes));
                if (generation != _soundFontLoadGeneration || _previewDisposed) return;
                _previewPlayer ??= new NotePreviewPlayer();
                _previewPlayer.SetRenderer(renderer);
                _previewFontReady = true;
                SoundStartupProbe.PreviewReady();
                TryPlayPendingPreview();
            }
            catch (Exception ex)
            {
                App.Log(ex);
                ShowToast("Anteprima non disponibile. La riproduzione del brano resta utilizzabile.", ToastKind.Warning);
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Dialogs.Warning(Window.GetWindow(this), "Non riesco a caricare il SoundFont:\n" + ex.Message);
        }
    }

    private bool _announceSoundFont;

    private void OnSoundFontLoaded()
    {
        if (!_soundFontLoads.TryDequeue(out var generation) || generation != _soundFontLoadGeneration) return;
        _soundFontReady = true;
        SoundStartupProbe.PlayerReady();
        TryPlayPendingPreview();
        if (_announceSoundFont) ShowToast($"Suoni caricati: {_soundFontName}", ToastKind.Ok);
        else StatusText.Text = $"Motore audio pronto · suoni: {_soundFontName}";
        _announceSoundFont = false;
    }

    private void OnSoundFontFailed()
    {
        if (!_soundFontLoads.TryDequeue(out var generation) || generation != _soundFontLoadGeneration) return;
        var wasCustom = AppSettings.Current.SoundFontPath != null;
        ShowToast(wasCustom
            ? $"Il SoundFont «{_soundFontName}» non si può usare: torno a quello incluso."
            : "Il SoundFont incluso non si è caricato: la riproduzione non funzionerà.", ToastKind.Warning);
        if (wasCustom) LoadSoundFont(null, false);
    }

    /// <summary>Un SoundFont è un file RIFF di tipo "sfbk".</summary>
    private static bool LooksLikeSoundFont(byte[] b) =>
        b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
        b[8] == 's' && b[9] == 'f' && b[10] == 'b' && b[11] == 'k';

    private void ChooseSoundFont_Click(object sender, RoutedEventArgs e)
    {
        var current = AppSettings.Current.SoundFontPath;
        var dlg = new OpenFileDialog
        {
            Title = "Scegli un SoundFont General MIDI",
            Filter = "SoundFont (*.sf2;*.sf3)|*.sf2;*.sf3|Tutti i file|*.*",
            InitialDirectory = current != null ? Path.GetDirectoryName(current) : null
        };
        if (dlg.ShowDialog() == true) LoadSoundFont(dlg.FileName, announce: true);
        Focus();
    }

    private void DefaultSoundFont_Click(object sender, RoutedEventArgs e)
    {
        LoadSoundFont(null, announce: true);
        Focus();
    }

    // ================================================================ esportazione

    private void ExportMidi_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Score is not { } score) return;
        var dlg = new SaveFileDialog
        {
            Title = "Esporta in MIDI",
            FileName = SafeFileName(score.Title),
            Filter = "File MIDI (*.mid)|*.mid",
            InitialDirectory = _currentPath != null ? Path.GetDirectoryName(_currentPath) : null
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.WriteAllBytes(dlg.FileName, _editor.ExportMidi());
            ShowToast("MIDI esportato: " + Path.GetFileName(dlg.FileName), ToastKind.Ok);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Dialogs.Warning(Window.GetWindow(this), "Esportazione non riuscita:\n" + ex.Message);
        }
        Focus();
    }

    private static string SafeFileName(string? title)
    {
        var name = string.IsNullOrWhiteSpace(title) ? "Tablatura" : title.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    // ================================================================ pannelli laterali

    private const double PaletteMinWidth = 190;
    private const double InspectorMinWidth = 250;
    private double _paletteWidth = 226;
    private double _inspectorWidth = 290;

    /// <summary>Colonne del pannello tracce: le stesse per l'intestazione e per ogni riga.</summary>
    private static readonly double[] TrackColumnWidths = { 4, 204, 34, 34, 124, 14 };

    private static void AddTrackColumns(Grid grid)
    {
        foreach (var w in TrackColumnWidths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    private void InitLayout()
    {
        AddTrackColumns(TracksHeader);
        var s = AppSettings.Current;
        _paletteWidth = Math.Clamp(s.PaletteWidth, PaletteMinWidth, PaletteColumn.MaxWidth);
        _inspectorWidth = Math.Clamp(s.InspectorWidth, InspectorMinWidth, InspectorColumn.MaxWidth);
        PalettePanel.Visibility = InspectorPanel.Visibility = Visibility.Collapsed;
        ShowPalette(PaletteToggle.IsChecked == true);
        ShowInspectorPanel(InspectorToggle.IsChecked == true);
        InitializeAdaptiveToolbar();
        InitializePanelHeights(s);
    }

    private void ShowPalette(bool show) =>
        ShowSidePanel(PaletteColumn, PalettePanel, PaletteSplitter, show, ref _paletteWidth, PaletteMinWidth);

    private void ShowInspectorPanel(bool show) =>
        ShowSidePanel(InspectorColumn, InspectorPanel, InspectorSplitter, show, ref _inspectorWidth, InspectorMinWidth);

    private static void ShowSidePanel(ColumnDefinition column, UIElement panel, UIElement splitter, bool show,
        ref double width, double minWidth)
    {
        // Ricorda la larghezza scelta dall'utente prima di nascondere il pannello.
        if (panel.Visibility == Visibility.Visible && column.ActualWidth >= minWidth) width = column.ActualWidth;
        panel.Visibility = splitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        column.MinWidth = show ? minWidth : 0;
        column.Width = new GridLength(show ? width : 0);
    }

    /// <summary>Salva nelle preferenze la larghezza dei pannelli (chiamato alla chiusura).</summary>
    public void StoreLayout(AppSettings settings)
    {
        if (PalettePanel.Visibility == Visibility.Visible && PaletteColumn.ActualWidth >= PaletteMinWidth)
            _paletteWidth = PaletteColumn.ActualWidth;
        if (InspectorPanel.Visibility == Visibility.Visible && InspectorColumn.ActualWidth >= InspectorMinWidth)
            _inspectorWidth = InspectorColumn.ActualWidth;
        settings.PaletteWidth = _paletteWidth;
        settings.InspectorWidth = _inspectorWidth;
        settings.FretboardHeight = _fretboardHeight;
        settings.TracksHeight = _tracksHeight;
    }
}
