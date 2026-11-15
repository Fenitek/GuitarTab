using System.Windows;
using System.Windows.Input;
using GuitarStudio.Editing;
using GuitarStudio.Ui;
using GuitarStudio.Views;

namespace GuitarStudio;

public partial class MainWindow : Window
{
    private HelpWindow? _help;

    public MainWindow()
    {
        InitializeComponent();
        WindowTheme.Apply(this);
        VersionText.Text = AppInfo.VersionLabel;
        ApplySavedBounds();
        if (Environment.GetEnvironmentVariable("GS_SELFTEST") == "1") ShowActivated = false;

        // Tablatura e foglio di accordi sono due documenti indipendenti: alla chiusura si chiede per entrambi.
        Closing += (_, e) =>
        {
            if (!PracticePage.ConfirmDiscard() || !SheetPage.ConfirmDiscard())
            {
                e.Cancel = true;
                return;
            }
            SaveBounds();
        };
        Closed += (_, _) =>
        {
            PracticePage.CompleteAutosave();
            SheetPage.CompleteAutosave();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F1) return;
            ShowHelp();
            e.Handled = true;
        };

        // Il recupero dei fogli segue quello delle tablature: mai due dialoghi insieme.
        PracticePage.Ready += OnPracticeReady;
        PracticePage.ChordSheetSelfTest = RunChordSheetSelfTest;

        // Trascinare una tablatura o un foglio di accordi sulla finestra lo apre.
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
            OpenDocument(files[0]);
        };
    }

    private void OnPracticeReady()
    {
        if (SheetPage.TryRecoverAutosave())
        {
            NavSheet.IsChecked = true;
            return;
        }
        if (App.StartupFile is { } startup && ChordSheetFile.IsChordSheetPath(startup) && SheetPage.OpenPath(startup, confirm: false))
            NavSheet.IsChecked = true;
    }

    /// <summary>Apre un file nella sezione giusta: fogli di accordi in Accordi, il resto in Tab.</summary>
    public bool OpenDocument(string path)
    {
        if (ChordSheetFile.IsChordSheetPath(path))
        {
            NavSheet.IsChecked = true;
            return SheetPage.OpenPath(path);
        }
        NavPractice.IsChecked = true;
        return PracticePage.OpenPath(path);
    }

    /// <summary>File → Nuovo foglio di accordi (dalla tablatura o dal foglio stesso).</summary>
    public void NewChordSheet()
    {
        NavSheet.IsChecked = true;
        SheetPage.NewSheet();
    }

    /// <summary>File → Nuova tablatura dal foglio di accordi.</summary>
    public void NewTablature()
    {
        NavPractice.IsChecked = true;
        PracticePage.NewTablature();
    }

    public void ShowHelp()
    {
        if (_help is { IsLoaded: true })
        {
            _help.Activate();
            return;
        }
        _help = new HelpWindow { Owner = this };
        _help.Show();
    }

    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PracticePage == null || SheetPage == null) return; // durante InitializeComponent

        PracticePage.Visibility = sender == NavPractice ? Visibility.Visible : Visibility.Collapsed;
        SheetPage.Visibility = sender == NavSheet ? Visibility.Visible : Visibility.Collapsed;
        TrainerPage.Visibility = sender == NavTrainer ? Visibility.Visible : Visibility.Collapsed;
        TranscribePage.Visibility = sender == NavTranscribe ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = sender == NavLibrary ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavPractice)
        {
            PracticePage.Focus();
            PracticePage.RefreshWindowTitle();
        }
        else if (sender == NavSheet)
        {
            SheetPage.Focus();
            SheetPage.UpdateTitle();
        }
    }

    private async Task RunChordSheetSelfTest(Action<string> log, Diagnostics.VisualCapture? capture, bool layout)
    {
        NavSheet.IsChecked = true;
        try { await SheetPage.RunSelfTest(log, capture, layout); }
        finally { NavPractice.IsChecked = true; }
    }

    // Posizione e dimensione della finestra ---------------------------

    private void ApplySavedBounds()
    {
        var s = AppSettings.Current;
        if (s.WindowWidth is { } w && s.WindowHeight is { } h && s.WindowLeft is { } l && s.WindowTop is { } t)
        {
            // Si ripristina solo se la finestra cade ancora dentro uno schermo collegato.
            var visible = l + 120 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                          l + w - 120 > SystemParameters.VirtualScreenLeft &&
                          t >= SystemParameters.VirtualScreenTop - 10 &&
                          t + 60 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
            if (visible)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l;
                Top = t;
                Width = Math.Max(MinWidth, w);
                Height = Math.Max(MinHeight, h);
            }
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveBounds()
    {
        var s = AppSettings.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        s.WindowLeft = r.Left;
        s.WindowTop = r.Top;
        s.WindowWidth = r.Width;
        s.WindowHeight = r.Height;
        PracticePage.StoreLayout(s);
        s.Save();
    }
}
