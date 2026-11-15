using System.IO;
using System.Text;
using System.Windows;
using GuitarStudio.Editing;
using GuitarStudio.Ui;
using Microsoft.Win32;

namespace GuitarStudio.Views;

/// <summary>
/// Strumenti del foglio (v0.11): trasposizione, capotasto, nomi italiani o internazionali,
/// diagrammi in cima al foglio, importazione ed esportazione ChordPro. La logica è nell'editor.
/// </summary>
public partial class ChordSheetView
{
    /// <summary>Stato di pulsanti e caselle secondo il foglio e le preferenze.</summary>
    private void SyncTools()
    {
        var notation = _editor.DominantNotation();
        NotationItalianToggle.IsChecked = notation == ChordNotation.Italian;
        NotationInternationalToggle.IsChecked = notation == ChordNotation.International;
        DiagramsCheck.IsChecked = AppSettings.Current.ShowChordDiagrams;
        CapoAdaptCheck.IsChecked = AppSettings.Current.CapoKeepsSound;
    }

    private void TransposeDown_Click(object sender, RoutedEventArgs e) => Transpose(-1);
    private void TransposeUp_Click(object sender, RoutedEventArgs e) => Transpose(+1);

    internal void Transpose(int semitones)
    {
        CommitInfo();
        if (!_editor.Transpose(semitones))
        {
            SetStatus("Nel foglio non ci sono accordi da trasporre.");
            return;
        }
        var key = string.IsNullOrWhiteSpace(_editor.Sheet.Key) ? "" : $": tonalità {DisplayChord(_editor.Sheet.Key)}";
        SetStatus($"Trasposto di {(semitones > 0 ? "+" : "−")}{Math.Abs(semitones)} semitono{key}. Ctrl+Z torna indietro.");
    }

    private void SetCapo(int capo)
    {
        var keep = AppSettings.Current.CapoKeepsSound;
        if (!_editor.SetCapo(capo, keep)) return;
        SetStatus(capo == 0
            ? keep ? "Capotasto tolto: gli accordi tornano alle forme senza capotasto." : "Capotasto tolto."
            : keep ? $"Capotasto al {capo}° tasto: gli accordi sono le forme da suonare, il brano suona uguale."
                   : $"Capotasto al {capo}° tasto: gli accordi non sono cambiati.");
    }

    private void CapoAdapt_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.CapoKeepsSound = CapoAdaptCheck.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void Notation_Click(object sender, RoutedEventArgs e)
    {
        var notation = (string)((FrameworkElement)sender).Tag == "it" ? ChordNotation.Italian : ChordNotation.International;
        SetNotation(notation);
    }

    internal void SetNotation(ChordNotation notation)
    {
        CommitInfo();
        SetStatus(_editor.ConvertNotation(notation)
            ? notation == ChordNotation.Italian ? "Accordi con i nomi italiani: Do, Re, Mi…" : "Accordi con i nomi internazionali: C, D, E…"
            : "Gli accordi hanno già questi nomi.");
        SyncTools();
        Focus();
    }

    private void Diagrams_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.ShowChordDiagrams = DiagramsCheck.IsChecked == true;
        AppSettings.Current.Save();
        RenderPaper();
    }

    /// <summary>
    /// Clic su un diagramma dell'anteprima: lo stesso dialogo degli accordi della tablatura, con
    /// i tasti dalla corda più grave. «Forma proposta» torna al diagramma automatico.
    /// </summary>
    internal void EditDiagram(string name)
    {
        var initial = _editor.Sheet.DiagramFor(name) ?? new ChordInfo(name, true, new int?[6]);
        var (result, chord) = Dialogs.Chord(Owner, initial with { Name = DisplayChord(name) }, _editor.Sheet.Diagrams.ContainsKey(name),
            message: "Il diagramma compare in cima al foglio per tutte le volte che usi questo accordo.",
            removeLabel: "Forma proposta", sheetDiagram: true);
        if (result == MessageBoxResult.Yes && chord != null)
        {
            if (_editor.SetDiagram(name, chord.FretsLowToHigh)) SetStatus($"Diagramma di {DisplayChord(name)} aggiornato.");
        }
        else if (result == MessageBoxResult.No && _editor.SetDiagram(name, null))
            SetStatus($"{DisplayChord(name)} torna al diagramma proposto.");
    }

    // ================================================================ ChordPro

    private const string ChordProFilter = "ChordPro (*.cho;*.chordpro;*.chopro;*.crd;*.pro)|*.cho;*.chordpro;*.chopro;*.crd;*.pro|Testo (*.txt)|*.txt|Tutti i file|*.*";

    private void ImportChordPro_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Title = "Importa un file ChordPro", Filter = ChordProFilter };
        if (dlg.ShowDialog() == true) ImportChordPro(dlg.FileName);
    }

    /// <summary>Il foglio importato è un documento nuovo, da salvare come «.accordi».</summary>
    internal bool ImportChordPro(string path)
    {
        ChordSheet sheet;
        try { sheet = ChordProFormat.Import(File.ReadAllText(path, Encoding.UTF8)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Log(ex);
            Dialogs.Warning(Owner, $"Non riesco a leggere «{Path.GetFileName(path)}».\n\n{ex.Message}");
            return false;
        }
        if (sheet.Sections.Count == 0 && sheet.Title.Length == 0)
        {
            Dialogs.Warning(Owner, $"In «{Path.GetFileName(path)}» non ho trovato testo, accordi o direttive ChordPro.");
            return false;
        }
        if (sheet.Title.Length == 0) sheet = sheet with { Title = Path.GetFileNameWithoutExtension(path) };
        ClearAutosave();
        _currentPath = null;
        _activeSection = 0;
        _editor.Load(sheet, isDirty: true);
        SetMode(false);
        SetStatus($"Importato da «{Path.GetFileName(path)}»: {sheet.Sections.Count} sezioni. Salva per creare il file .accordi.");
        return true;
    }

    private void ExportChordPro_Click(object sender, RoutedEventArgs e)
    {
        CommitInfo();
        var dlg = new SaveFileDialog
        {
            Title = "Esporta in ChordPro",
            FileName = Path.ChangeExtension(SafeFileName(_editor.Sheet.Title), ".cho"),
            DefaultExt = ".cho",
            Filter = ChordProFilter
        };
        if (dlg.ShowDialog() == true) ExportChordPro(dlg.FileName);
    }

    /// <summary>Esporta senza cambiare il file del foglio né il segno delle modifiche.</summary>
    internal bool ExportChordPro(string path)
    {
        try
        {
            File.WriteAllText(path, ChordProFormat.Export(_editor.Sheet), new UTF8Encoding(false));
            SetStatus("Esportato in ChordPro: " + Path.GetFileName(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Log(ex);
            Dialogs.Warning(Owner, "Esportazione non riuscita:\n" + ex.Message);
            return false;
        }
    }
}
