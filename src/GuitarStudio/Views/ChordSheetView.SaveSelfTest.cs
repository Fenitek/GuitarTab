using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuitarStudio.Editing;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest del salvataggio sicuro del foglio: Ctrl+S e «Salva con nome» con accenti, accordi e
/// diagrammi; un «&lt;file&gt;.tmp» dell'utente non va toccato; destinazione bloccata; riapertura.
/// </summary>
public partial class ChordSheetView
{
    private async Task VerifySafeSave(Action<bool, string> require)
    {
        void Require(bool condition, string message) => require(condition, "salvataggio sicuro: " + message);

        var directory = Path.Combine(Path.GetTempPath(), $"gs_safesave_accordi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Perché è così" + ChordSheetFile.Extension);
        var other = Path.Combine(directory, "Città già scritta" + ChordSheetFile.Extension);
        // File dell'utente con il nome che usava il vecchio temporaneo fisso: deve restare identico.
        var sentinel = path + ".tmp";
        var sentinelBytes = Encoding.UTF8.GetBytes("sentinella: però, perché, città — non toccare\n");
        File.WriteAllBytes(sentinel, sentinelBytes);
        bool SentinelIntact() => File.ReadAllBytes(sentinel).SequenceEqual(sentinelBytes);
        // Nessun temporaneo del salvataggio rimasto: i soli .tmp ammessi sono la sentinella.
        bool NoLeftovers() => Directory.GetFiles(directory, "*.tmp").All(f => f == sentinel);
        bool RecoveryHas(string text) => AutosavePath is { } copy && File.Exists(copy) &&
            ChordSheetFile.Read(copy).Sections.Any(s => s.Body.Contains(text, StringComparison.Ordinal));

        var warnings = new List<string>();
        var previousPresenter = Dialogs.SelfTestPresenter;
        Dialogs.SelfTestPresenter = window =>
        {
            warnings.Add(string.Join(" ", DialogTexts(window)));
            return MessageBoxResult.OK;
        };
        var recent = AppSettings.Current.RecentFiles.ToList();
        try
        {
            NewSheet(confirm: false);
            var sheet = new ChordSheet("Perché è così", "Niccolò Città", "Re", 2, ImmutableList.Create(
                new SheetSection("Strofa", SheetSectionKind.Lyrics, "[Re]Però la città [Sol/Si]è già là\n[Lam7]perché così"),
                new SheetSection("Giro dell'intro", SheetSectionKind.Progression, "| Re | Sol/Si | Lam7 Do |")))
            {
                Diagrams = ImmutableSortedDictionary<string, string>.Empty
                    .Add("Lam7", "x 0 2 0 1 0").Add("Sol/Si", "x 2 0 0 3 3")
            };
            _editor.Load(sheet);
            _editor.SetSectionBody(0, sheet.Sections[0].Body + "\n[Do]ancora qui");
            await Task.Delay(100);

            // 1. «Salva con nome» (Ctrl+Maiusc+S) su un foglio nuovo.
            _selfTestSavePath = _ => path;
            await PressKey(Key.S, ModifierKeys.Control | ModifierKeys.Shift);
            var expected = _editor.Sheet;
            Require(_currentPath == path && !_editor.IsDirty && ChordSheetFile.Read(path).SameContent(expected),
                "Salva con nome scrive accenti, accordi e diagrammi");
            Require(File.ReadAllText(path, Encoding.UTF8).Contains("Perché è così", StringComparison.Ordinal) &&
                    expected.Diagrams.Count == 2, "accenti leggibili nel file e due diagrammi");
            Require(SentinelIntact() && NoLeftovers(), "Salva con nome lascia intatto «<file>.tmp» e nessun temporaneo");

            // 2. Ctrl+S dopo una modifica.
            _selfTestSavePath = null;
            _editor.SetSectionBody(0, _editor.Sheet.Sections[0].Body + "\n[Mi]più giù");
            await PressKey(Key.S, ModifierKeys.Control);
            Require(!_editor.IsDirty && ChordSheetFile.Read(path).SameContent(_editor.Sheet), "Ctrl+S sostituisce il foglio");
            Require(SentinelIntact() && NoLeftovers(), "Ctrl+S lascia intatto «<file>.tmp» e nessun temporaneo");
            var saved = File.ReadAllBytes(path);

            // 3. Ctrl+S con la destinazione bloccata da un altro programma.
            _editor.SetSectionBody(0, _editor.Sheet.Sections[0].Body + "\n[La]ultima modifica");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                await PressKey(Key.S, ModifierKeys.Control);
            Require(warnings.Count == 1 && warnings[0].Contains("non è stato modificato", StringComparison.Ordinal),
                "avviso sul Ctrl+S fallito");
            Require(File.ReadAllBytes(path).SequenceEqual(saved), "Ctrl+S fallito: originale integro");
            Require(_editor.IsDirty && _currentPath == path, "Ctrl+S fallito: foglio modificato sul percorso precedente");
            Require(RecoveryHas("ultima modifica"), "Ctrl+S fallito: copia di recupero aggiornata subito");
            Require(SentinelIntact() && NoLeftovers(), "Ctrl+S fallito: «<file>.tmp» intatto, nessun temporaneo");

            // 4. «Salva con nome» verso un altro foglio bloccato.
            File.WriteAllBytes(other, saved);
            _editor.SetSectionBody(1, _editor.Sheet.Sections[1].Body + "\n| Mi | La |");
            _selfTestSavePath = _ => other;
            using (new FileStream(other, FileMode.Open, FileAccess.Read, FileShare.Read))
                await PressKey(Key.S, ModifierKeys.Control | ModifierKeys.Shift);
            Require(warnings.Count == 2 && File.ReadAllBytes(other).SequenceEqual(saved) && File.ReadAllBytes(path).SequenceEqual(saved),
                "Salva con nome fallito: entrambi i file integri");
            Require(_editor.IsDirty && _currentPath == path, "Salva con nome fallito: resta il percorso precedente, modificato");
            Require(RecoveryHas("| Mi | La |"), "Salva con nome fallito: copia di recupero aggiornata subito");
            Require(SentinelIntact() && NoLeftovers(), "Salva con nome fallito: «<file>.tmp» intatto, nessun temporaneo");

            // 5. Sbloccato: Ctrl+S riesce, la copia sparisce, la riapertura mostra le ultime modifiche.
            _selfTestSavePath = null;
            var latest = _editor.Sheet;
            var copy = AutosavePath;
            await PressKey(Key.S, ModifierKeys.Control);
            Require(!_editor.IsDirty && warnings.Count == 2 && (copy == null || !File.Exists(copy)),
                "Ctrl+S dopo lo sblocco riesce ed elimina la copia di recupero");
            NewSheet(confirm: false);
            Require(OpenPath(path, confirm: false) && _editor.Sheet.SameContent(latest) &&
                    _editor.Sheet.Sections[0].Body.EndsWith("[La]ultima modifica", StringComparison.Ordinal) &&
                    _editor.Sheet.Sections[1].Body.EndsWith("| Mi | La |", StringComparison.Ordinal) &&
                    _editor.Sheet.Diagrams["Lam7"] == "x 0 2 0 1 0" && _editor.Sheet.Title == "Perché è così",
                "la riapertura mostra le ultime modifiche, accenti e diagrammi");
            Require(SentinelIntact() && NoLeftovers(), "a fine prova «<file>.tmp» ha gli stessi byte");
        }
        finally
        {
            Dialogs.SelfTestPresenter = previousPresenter;
            _selfTestSavePath = null;
            _editor.MarkSaved();
            NewSheet(confirm: false);
            AppSettings.Current.RecentFiles = recent;
            AppSettings.Current.Save();
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
        }
    }

    private static IEnumerable<string> DialogTexts(DependencyObject root)
    {
        if (root is TextBlock { Text: { Length: > 0 } text }) yield return text;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var t in DialogTexts(child)) yield return t;
    }
}
