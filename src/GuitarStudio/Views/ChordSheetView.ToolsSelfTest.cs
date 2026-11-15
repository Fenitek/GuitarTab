using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>Autotest degli strumenti del foglio (v0.11): trasposizione, capotasto, nomi, diagrammi, ChordPro.</summary>
public partial class ChordSheetView
{
    private string Body(int section) => _editor.Sheet.Sections[section].Body;

    private async Task VerifyTools(Action<bool, string> require, string directory)
    {
        const string intro = "| Sol Sol7 | Re | Mim Do |";
        require(Body(0) == intro && _editor.Sheet.Key == "Sol", "foglio di partenza in Sol");

        await PressKey(Key.Up, ModifierKeys.Control | ModifierKeys.Shift);
        require(_editor.Sheet.Key == "Lab" && Body(0) == "| Lab Lab7 | Mib | Fam Reb |" && Body(2).Contains("[Mib]selva", StringComparison.Ordinal),
            "Ctrl+Maiusc+↑: da Sol a La♭ maggiore, accordi con i bemolle");
        require(_cards[0].Body.Text == Body(0) && KeyBox.Text == "Lab", "riquadri e tonalità aggiornati");
        await PressKey(Key.Down, ModifierKeys.Control | ModifierKeys.Shift);
        require(_editor.Sheet.Key == "Sol" && Body(0) == intro, "Ctrl+Maiusc+↓ torna in Sol");
        TransposeUpButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await PressKey(Key.Z, ModifierKeys.Control);
        require(Body(0) == intro, "pulsante ♯ +1 e Ctrl+Z");

        AppSettings.Current.CapoKeepsSound = true;
        CapoCombo.SelectedIndex = 2;
        require(_editor.Sheet.Capo == 2 && _editor.Sheet.Key == "Sol" && Body(0) == "| Fa Fa7 | Do | Rem Sib |",
            "capotasto 2: forme di Fa, tonalità reale Sol");
        CapoCombo.SelectedIndex = 0;
        require(_editor.Sheet.Capo == 0 && Body(0) == intro, "capotasto tolto: forme di partenza");

        NotationInternationalToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        require(Body(0) == "| G G7 | D | Em C |" && _editor.Sheet.Key == "G" && NotationInternationalToggle.IsChecked == true &&
                NotationItalianToggle.IsChecked == false, "C D E: nomi internazionali");
        NotationItalianToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        require(Body(0) == intro && _editor.Sheet.Key == "Sol", "Do Re Mi: di nuovo nomi italiani");

        // Diagrammi: in lettura uno per accordo usato; nell'anteprima un clic apre il dialogo degli accordi.
        AppSettings.Current.ShowChordDiagrams = true;
        SetMode(true);
        await VisualCapture.Paint();
        var diagrams = VisualCapture.Descendants<ChordDiagramView>((DependencyObject)ReadScroll.Content).Select(d => d.ChordName).ToList();
        require(diagrams.SequenceEqual(_editor.Sheet.UsedChords()) && diagrams.Count == 5, "in lettura i diagrammi degli accordi usati in cima");
        var keyLine = VisualCapture.Descendants<TextBlock>((DependencyObject)ReadScroll.Content).Any(t => t.Text == "Tonalità Sol");
        require(keyLine, "tonalità in testa al foglio");
        SetMode(false);

        var previous = Dialogs.SelfTestPresenter;
        try
        {
            Dialogs.SelfTestPresenter = window =>
            {
                ((TextBox)LogicalTreeHelper.FindLogicalNode(window, "ChordFretsBox")!).Text = "3 x 0 0 3 3";
                return MessageBoxResult.Yes;
            };
            EditDiagram("Sol");
            require(_editor.Sheet.Diagrams.TryGetValue("Sol", out var frets) && frets == "3 x 0 0 3 3", "diagramma di Sol scelto con il dialogo degli accordi");
            Dialogs.SelfTestPresenter = _ => MessageBoxResult.No;
            EditDiagram("Sol");
            require(!_editor.Sheet.Diagrams.ContainsKey("Sol"), "«Forma proposta» torna al diagramma automatico");
        }
        finally { Dialogs.SelfTestPresenter = previous; }

        // ChordPro: esporta, reimporta, stesso contenuto (documento nuovo da salvare).
        var expected = _editor.Sheet;
        var cho = Path.Combine(directory, "Canzone di prova.cho");
        require(ExportChordPro(cho) && File.ReadAllText(cho).Contains("{start_of_grid: label=\"Intro\"}", StringComparison.Ordinal),
            "esportazione ChordPro");
        require(ImportChordPro(cho) && _editor.Sheet.SameContent(expected) && _editor.IsDirty && DocTitle.Text.StartsWith("Non salvato", StringComparison.Ordinal),
            "importazione ChordPro identica, come foglio nuovo da salvare");
    }

    /// <summary>Foto del dialogo del diagramma aperto dall'anteprima.</summary>
    private void CaptureDiagramDialog(VisualCapture capture, Window main, string prefix)
    {
        var previous = Dialogs.SelfTestPresenter;
        Dialogs.SelfTestPresenter = window =>
        {
            window.ShowActivated = false;
            window.Show();
            window.Left = main.Left + (main.ActualWidth - window.ActualWidth) / 2;
            window.Top = main.Top + (main.ActualHeight - window.ActualHeight) / 2;
            VisualCapture.PaintSynchronously();
            capture.Save(main, prefix + "foglio-dialogo-diagramma", window);
            window.Close();
            return MessageBoxResult.Cancel;
        };
        try { EditDiagram("Mim"); }
        finally { Dialogs.SelfTestPresenter = previous; }
    }
}
