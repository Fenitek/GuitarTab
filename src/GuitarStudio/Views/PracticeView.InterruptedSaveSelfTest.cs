using System.IO;
using System.Windows;
using System.Windows.Input;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Model;
using GuitarStudio.Recovery;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest di un salvataggio interrotto a metà scrittura, per .gp e .atex: il file già salvato
/// deve restare identico byte per byte, la modifica deve restare aperta, un nuovo Ctrl+S deve
/// riuscire e la riapertura del file deve mostrare la nota modificata.
/// </summary>
public partial class PracticeView
{
    private async Task VerifyInterruptedSave(LiveLog log)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gs_interrupted_save_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var warnings = new List<string>();
        var previousPresenter = Dialogs.SelfTestPresenter;
        Dialogs.SelfTestPresenter = window =>
        {
            warnings.Add(string.Join(" ", Texts(window)));
            return MessageBoxResult.OK;
        };
        var recent = AppSettings.Current.RecentFiles.ToList();
        try
        {
            await VerifyInterruptedSave(log, warnings, Path.Combine(directory, "interrotto.gp"), before: 3, after: 7);
            await VerifyInterruptedSave(log, warnings, Path.Combine(directory, "interrotto.atex"), before: 5, after: 9);
        }
        finally
        {
            Dialogs.SelfTestPresenter = previousPresenter;
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

    private async Task VerifyInterruptedSave(LiveLog log, List<string> warnings, string path, int before, int after)
    {
        var format = Path.GetExtension(path);
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"salvataggio interrotto {format}: {message}");
            log.AppendLine($"salvataggio interrotto {format} OK: {message}");
        }

        async Task CtrlS()
        {
            _testModifiers = ModifierKeys.Control;
            RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.S)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            _testModifiers = null;
            await Task.Delay(150);
        }

        var directory = Path.GetDirectoryName(path)!;
        string[] Leftovers() => Directory.GetFiles(directory, "*.tmp");
        int? FretIn(Score score, int track, int modelString) =>
            score.Tracks[track].Staves[0].Bars[0].Voices[0].Beats[0].GetNoteOnString(modelString)?.Fret is { } f ? (int)f : null;

        // 1. Una tablatura già salvata con una nota nota: prima battuta, primo colpo, corda 1.
        _editor.MoveTo(0, 0, 1);
        var track = _editor.TrackIndex;
        var modelString = _editor.ModelString(1);
        _editor.SetFret(before);
        _currentPath = path;
        await CtrlS();
        Require(File.Exists(path) && !_editor.IsDirty && warnings.Count == 0 && Leftovers().Length == 0,
            $"tablatura salvata con la nota {before}");
        var original = File.ReadAllBytes(path);
        log.AppendLine($"salvataggio interrotto {format}: originale di {original.Length} byte");

        // 2. Si modifica la nota e il salvataggio si rompe dopo aver già scritto parte del temporaneo.
        _editor.SetFret(after);
        Require(_editor.IsDirty && _editor.CurrentNote?.Fret == after, $"nota portata a {after}, brano «modificato»");
        var fault = SafeFile.ArmSelfTestFault(afterBytes: Math.Max(1, original.Length / 2));
        await CtrlS();
        log.AppendLine($"salvataggio interrotto {format}: guasto dopo {fault.BytesOnDisk} byte su disco in «{Path.GetFileName(fault.TemporaryPath)}»");
        Require(fault.Triggered && fault.BytesOnDisk > 0 && fault.TemporaryPath != null &&
                Path.GetDirectoryName(fault.TemporaryPath) == directory, "la scrittura era iniziata nel temporaneo della stessa cartella");
        Require(warnings.Count == 1 && warnings[0].Contains("non è stato modificato", StringComparison.Ordinal) &&
                warnings[0].Contains("simulato", StringComparison.Ordinal), "avviso con file intatto e motivo");
        var current = File.ReadAllBytes(path);
        Require(current.Length == original.Length && current.AsSpan().SequenceEqual(original),
            $"il file su disco è identico all'originale byte per byte ({current.Length} byte)");
        Require(!File.Exists(fault.TemporaryPath) && Leftovers().Length == 0, "il temporaneo a metà è stato eliminato");
        Require(_editor.IsDirty && _currentPath == path && _editor.CurrentNote?.Fret == after,
            $"la modifica è ancora aperta (nota {after}, «modificato», stesso percorso)");
        Require(_autosave != null && File.Exists(_autosave.FilePath) &&
                FretIn(ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(_autosave.FilePath)), Api.Settings), track, modelString) == after,
            $"la copia di recupero contiene la nota {after}");

        // 3. Nuovo tentativo, senza guasto: riesce e sostituisce il file.
        await CtrlS();
        Require(!_editor.IsDirty && warnings.Count == 1 && Leftovers().Length == 0 && !File.ReadAllBytes(path).AsSpan().SequenceEqual(original),
            "il secondo Ctrl+S riesce e sostituisce il file");
        warnings.Clear();

        // 4. Riapertura dal disco, come da «Apri»: la nota letta è quella modificata.
        var previous = _editor.Score;
        Require(OpenPath(path, confirm: false), "il file si riapre");
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while ((_editor.Score == previous || _expectingLoad) && DateTime.UtcNow < deadline)
            await Task.Delay(100);
        Require(_editor.Score != previous && _currentPath == path && !_editor.IsDirty && warnings.Count == 0,
            "la partitura riaperta è quella del file");
        var reopened = FretIn(_editor.Score!, track, modelString);
        log.AppendLine($"salvataggio interrotto {format}: nota riaperta = {reopened?.ToString() ?? "nessuna"} (attesa {after})");
        Require(reopened == after, $"dopo la riapertura la nota è {after}");
    }
}
