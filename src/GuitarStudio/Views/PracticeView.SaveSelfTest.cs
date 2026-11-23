using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest del salvataggio sicuro: Ctrl+S e «Salva con nome» (Ctrl+Maiusc+S) in .gp e .atex,
/// riusciti e falliti. Un fallimento deve lasciare file, stato «modificato» e copia di recupero.
/// </summary>
public partial class PracticeView
{
    private async Task VerifySafeSave(LiveLog log)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("salvataggio sicuro: " + message);
            log.AppendLine("salvataggio sicuro OK: " + message);
        }

        async Task Press(ModifierKeys mods)
        {
            _testModifiers = mods;
            RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.S)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            _testModifiers = null;
            await Task.Delay(150);
        }

        var directory = Path.Combine(Path.GetTempPath(), $"gs_safesave_selftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string[] Leftovers() => Directory.GetFiles(directory, "*.tmp");
        bool RecoveryMatchesEditor() => _autosave != null && File.Exists(_autosave.FilePath) &&
            ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(_autosave.FilePath)), Api.Settings).Title == _editor.Score!.Title;

        var warnings = new List<string>();
        var previousPresenter = Dialogs.SelfTestPresenter;
        Dialogs.SelfTestPresenter = window =>
        {
            warnings.Add(string.Join(" ", Texts(window)));
            return MessageBoxResult.OK;
        };
        var recent = AppSettings.Current.RecentFiles.ToList();
        var gp = Path.Combine(directory, "brano.gp");
        var atex = Path.Combine(directory, "brano.atex");
        try
        {
            // 1. Ctrl+S su un .gp: scrittura completa, nessun temporaneo, documento pulito.
            _currentPath = gp;
            _editor.SetTitle("Salvataggio sicuro 1");
            await Press(ModifierKeys.Control);
            Require(File.Exists(gp) && !_editor.IsDirty && Leftovers().Length == 0, "Ctrl+S scrive il .gp senza lasciare temporanei");
            Require(ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(gp)), Api.Settings).Title == "Salvataggio sicuro 1",
                "il .gp salvato si rilegge");
            var saved = File.ReadAllBytes(gp);

            // 2. Ctrl+S fallito (file bloccato da un altro programma): tutto resta com'era.
            _editor.SetTitle("Salvataggio sicuro 2");
            using (new FileStream(gp, FileMode.Open, FileAccess.Read, FileShare.Read))
                await Press(ModifierKeys.Control);
            Require(warnings.Count == 1 && warnings[0].Contains("non è stato modificato", StringComparison.Ordinal) &&
                    warnings[0].Contains("copia di recupero", StringComparison.Ordinal), "avviso chiaro sul Ctrl+S fallito");
            Require(File.ReadAllBytes(gp).SequenceEqual(saved), "Ctrl+S fallito conserva il .gp originale");
            Require(_editor.IsDirty && _currentPath == gp, "Ctrl+S fallito conserva stato «modificato» e percorso");
            Require(RecoveryMatchesEditor(), "Ctrl+S fallito aggiorna la copia di recupero");
            Require(Leftovers().Length == 0, "Ctrl+S fallito non lascia temporanei");

            // 3. Salva con nome in .atex: nuovo percorso, testo completo, copia di recupero eliminata.
            _selfTestSavePath = _ => atex;
            await Press(ModifierKeys.Control | ModifierKeys.Shift);
            Require(_currentPath == atex && !_editor.IsDirty && File.ReadAllText(atex) == _editor.ExportAlphaTex(),
                "Salva con nome scrive l'.atex e lo rende il file corrente");
            Require(_autosave == null || !File.Exists(_autosave.FilePath), "Salva con nome riuscito elimina la copia di recupero");
            Require(File.ReadAllBytes(gp).SequenceEqual(saved) && Leftovers().Length == 0, "il vecchio .gp non viene toccato");
            var savedTex = File.ReadAllText(atex);

            // 4. Salva con nome sopra un .gp in sola lettura: fallisce senza danni.
            _editor.SetTitle("Salvataggio sicuro 3");
            File.SetAttributes(gp, FileAttributes.ReadOnly);
            try
            {
                _selfTestSavePath = _ => gp;
                await Press(ModifierKeys.Control | ModifierKeys.Shift);
            }
            finally { File.SetAttributes(gp, FileAttributes.Normal); }
            Require(warnings.Count == 2 && File.ReadAllBytes(gp).SequenceEqual(saved), "Salva con nome fallito conserva il file scelto");
            Require(_currentPath == atex && File.ReadAllText(atex) == savedTex, "Salva con nome fallito non cambia il file corrente");
            Require(_editor.IsDirty && RecoveryMatchesEditor() && Leftovers().Length == 0,
                "Salva con nome fallito conserva «modificato» e copia di recupero");

            // 5. Ctrl+S sull'.atex dopo il fallimento: si salva normalmente.
            _selfTestSavePath = null;
            await Press(ModifierKeys.Control);
            Require(!_editor.IsDirty && File.ReadAllText(atex) == _editor.ExportAlphaTex() && warnings.Count == 2 &&
                    Leftovers().Length == 0, "Ctrl+S successivo sostituisce l'.atex");
        }
        finally
        {
            Dialogs.SelfTestPresenter = previousPresenter;
            _selfTestSavePath = null;
            _currentPath = null;
            _editor.MarkSaved();
            UpdateTitle();
            AppSettings.Current.RecentFiles = recent;
            AppSettings.Current.Save();
            RebuildRecentMenu();
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
        }
    }

    private static IEnumerable<string> Texts(DependencyObject root)
    {
        if (root is TextBlock { Text: { Length: > 0 } text }) yield return text;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var t in Texts(child)) yield return t;
    }
}
