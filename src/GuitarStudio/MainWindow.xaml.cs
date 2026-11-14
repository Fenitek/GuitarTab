using System.Windows;
using System.Windows.Input;
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

        Closing += (_, e) =>
        {
            if (!PracticePage.ConfirmDiscard())
            {
                e.Cancel = true;
                return;
            }
            SaveBounds();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F1) return;
            ShowHelp();
            e.Handled = true;
        };

        // Trascinare una tablatura sulla finestra la apre.
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
            NavPractice.IsChecked = true;
            PracticePage.OpenPath(files[0]);
        };
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
        if (PracticePage == null) return; // durante InitializeComponent

        PracticePage.Visibility = sender == NavPractice ? Visibility.Visible : Visibility.Collapsed;
        TrainerPage.Visibility = sender == NavTrainer ? Visibility.Visible : Visibility.Collapsed;
        TranscribePage.Visibility = sender == NavTranscribe ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = sender == NavLibrary ? Visibility.Visible : Visibility.Collapsed;
        if (sender == NavPractice) PracticePage.Focus();
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
