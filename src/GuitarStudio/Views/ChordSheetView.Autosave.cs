using System.IO;
using System.Windows;
using System.Windows.Threading;
using GuitarStudio.Editing;
using GuitarStudio.Recovery;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>
/// Copia di recupero del foglio di accordi: stesso schema delle tablature (AutosaveStore, ogni
/// 60 secondi, solo con modifiche non salvate), ma file «.accordi» separati dalle copie «.gp».
/// </summary>
public partial class ChordSheetView
{
    private AutosaveStore? _autosave;
    private DispatcherTimer? _autosaveTimer;
    private int _autosavedRevision = -1;
    private bool _autosaveFailed;

    private void StartAutosave()
    {
        _autosaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(60) };
        _autosaveTimer.Tick += (_, _) => AutosaveNow();
        _autosaveTimer.Start();
    }

    /// <summary>Aggiorna la copia di recupero se il foglio è cambiato dall'ultima volta.</summary>
    internal bool AutosaveNow()
    {
        if (!_editor.IsDirty || _autosavedRevision == _editor.Revision) return false;
        try
        {
            _autosave ??= new AutosaveStore(extension: ChordSheetFile.Extension);
            if (!_autosave.SaveIfNeeded(_editor)) return false;
            _autosavedRevision = _editor.Revision;
            _autosaveFailed = false;
            SetStatus("Copia di recupero del foglio aggiornata alle " + DateTime.Now.ToString("HH:mm") + ". Usa Salva per conservarlo nel tuo file.");
            return true;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            if (!_autosaveFailed) SetStatus("La copia di recupero del foglio non è stata aggiornata. Salva con Ctrl+S.");
            _autosaveFailed = true;
            return false;
        }
    }

    internal string? AutosavePath => _autosave?.FilePath;

    private void ClearAutosave()
    {
        _autosavedRevision = -1;
        _autosaveFailed = false;
        try { _autosave?.Delete(); }
        catch (Exception ex)
        {
            App.Log(ex);
            SetStatus("Non riesco a eliminare la vecchia copia di recupero del foglio: potrebbe essere riproposta al prossimo avvio.");
        }
    }

    /// <summary>Solo dopo Closed: annullare la chiusura deve conservare timer e copia.</summary>
    public void CompleteAutosave()
    {
        _autosaveTimer?.Stop();
        ClearAutosave();
        _autosave?.Dispose();
        _autosave = null;
    }

    /// <summary>All'avvio, dopo il recupero delle tablature: propone le copie di fogli rimaste.</summary>
    public bool TryRecoverAutosave()
    {
        try
        {
            foreach (var candidate in AutosaveStore.FindRecoverable(extension: ChordSheetFile.Extension))
            {
                var adopted = false;
                try
                {
                    ChordSheet sheet;
                    try { sheet = ChordSheetFile.Read(candidate.FilePath); }
                    catch (Exception ex)
                    {
                        App.Log(ex);
                        Dialogs.Warning(Owner, "Una copia di recupero di un foglio di accordi non si può leggere. È stata conservata in:\n" + candidate.FilePath);
                        continue;
                    }
                    var title = string.IsNullOrWhiteSpace(sheet.Title) ? "Foglio senza titolo" : sheet.Title;
                    var date = File.GetLastWriteTime(candidate.FilePath).ToString("dd/MM/yyyy HH:mm");
                    var answer = Dialogs.RecoverAutosave(Owner,
                        $"Ho trovato una copia del foglio di accordi «{title}» del {date}, rimasta dopo un'interruzione.\n\n" +
                        "Vuoi recuperarla? Il file originale non verrà modificato.", "Recupera foglio di accordi");
                    if (answer == MessageBoxResult.No)
                    {
                        candidate.Delete();
                        continue;
                    }
                    if (answer != MessageBoxResult.Yes) continue;

                    _currentPath = null;
                    _editor.Load(sheet, isDirty: true);
                    _autosave = candidate;
                    adopted = true;
                    SetStatus("Foglio di accordi recuperato. Usa Salva per scegliere dove conservarlo.");
                    return true;
                }
                finally
                {
                    if (!adopted) candidate.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Dialogs.Warning(Owner, "Non riesco a controllare le copie di recupero dei fogli di accordi. Le copie esistenti sono state conservate.");
        }
        return false;
    }
}
