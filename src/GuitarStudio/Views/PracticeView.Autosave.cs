using System.IO;
using System.Windows;
using System.Windows.Threading;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using GuitarStudio.Recovery;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private AutosaveStore? _autosave;
    private DispatcherTimer? _autosaveTimer;
    private int _autosavedRevision = -1;
    private bool _autosaveFailed;

    private void StartAutosave()
    {
        _autosaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _autosaveTimer.Tick += AutosaveTick;
        _autosaveTimer.Start();
    }

    private void AutosaveTick(object? sender, EventArgs e)
    {
        if (_editor.Score == null || !_editor.IsDirty || _autosavedRevision == _revision) return;
        try
        {
            _autosave ??= new AutosaveStore();
            if (!_autosave.SaveIfNeeded(_editor)) return;
            _autosavedRevision = _revision;
            _autosaveFailed = false;
            StatusText.Text = "Copia di recupero aggiornata alle " + DateTime.Now.ToString("HH:mm") +
                              ". Usa Salva per conservare il brano nel tuo file.";
        }
        catch (Exception ex)
        {
            App.Log(ex);
            // Una sola notifica per il guasto; il timer riprova al giro successivo.
            if (!_autosaveFailed)
                ShowToast("La copia di recupero non è stata aggiornata. Salva il brano con Ctrl+S.", ToastKind.Warning);
            _autosaveFailed = true;
        }
    }

    private void ClearAutosave()
    {
        _autosavedRevision = -1;
        _autosaveFailed = false;
        try { _autosave?.Delete(); }
        catch (Exception ex)
        {
            App.Log(ex);
            ShowToast("Non riesco a eliminare la vecchia copia di recupero. Potrebbe essere riproposta al prossimo avvio.", ToastKind.Warning);
        }
    }

    /// <summary>Solo dopo Closed: annullare la chiusura deve conservare timer e copia.</summary>
    public void CompleteAutosave()
    {
        CancelPendingMidiReload();
        _autosaveTimer?.Stop();
        ClearAutosave();
        _autosave?.Dispose();
        _autosave = null;
    }

    private bool TryRecoverAutosave()
    {
        try
        {
            foreach (var candidate in AutosaveStore.FindRecoverable())
            {
                var adopted = false;
                try
                {
                    AlphaTab.Model.Score score;
                    try
                    {
                        score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(candidate.FilePath)), Api.Settings);
                    }
                    catch (Exception ex)
                    {
                        App.Log(ex);
                        Dialogs.Warning(Window.GetWindow(this),
                            "Una copia di recupero non si può leggere. È stata conservata in:\n" + candidate.FilePath);
                        continue;
                    }

                    var title = string.IsNullOrWhiteSpace(score.Title) ? "Tablatura senza titolo" : score.Title;
                    var date = File.GetLastWriteTime(candidate.FilePath).ToString("dd/MM/yyyy HH:mm");
                    var answer = Dialogs.RecoverAutosave(Window.GetWindow(this),
                        $"Ho trovato una copia di «{title}» del {date}, rimasta dopo un'interruzione.\n\n" +
                        "Vuoi recuperarla? Il file originale non verrà modificato.");
                    if (answer == MessageBoxResult.No)
                    {
                        candidate.Delete();
                        continue;
                    }
                    if (answer != MessageBoxResult.Yes) continue;

                    _currentPath = null; // Recupero come documento da salvare con un nome scelto dall'utente.
                    _expectingLoad = false;
                    _editor.Attach(score, 0, isDirty: true);
                    Render();
                    RebuildTracksPanel();
                    SetEditMode(true);
                    UpdateTitle();
                    UpdateEditUi();
                    ShowToast("Tablatura recuperata. Usa Salva per scegliere dove conservarla.", ToastKind.Ok);
                    _autosave = candidate;
                    adopted = true;
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
            Dialogs.Warning(Window.GetWindow(this),
                "Non riesco a controllare le copie di recupero. Le copie esistenti sono state conservate.");
        }
        return false;
    }
}
