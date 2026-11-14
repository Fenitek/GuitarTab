using System.Windows;

namespace GuitarStudio;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.VersionLabel;
        Closing += (_, e) =>
        {
            if (!PracticePage.ConfirmDiscard()) e.Cancel = true;
        };
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PracticePage == null) return; // durante InitializeComponent

        PracticePage.Visibility = sender == NavPractice ? Visibility.Visible : Visibility.Collapsed;
        TrainerPage.Visibility = sender == NavTrainer ? Visibility.Visible : Visibility.Collapsed;
        TranscribePage.Visibility = sender == NavTranscribe ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = sender == NavLibrary ? Visibility.Visible : Visibility.Collapsed;
    }
}
