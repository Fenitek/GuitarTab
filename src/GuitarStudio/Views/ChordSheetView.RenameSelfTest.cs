using System.Collections.Immutable;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest della rinomina di una sezione con il cursore ancora nel campo: su un foglio già
/// salvato «Strofa» diventa «Ritornello» e si preme Ctrl+S senza uscire dal campo. Il nome deve
/// finire nel file e nella copia di recupero, un solo Ctrl+Z deve riportare «Strofa» e la
/// riapertura del file deve mostrare «Ritornello».
/// </summary>
public partial class ChordSheetView
{
    private async Task VerifyRenameWhileTyping(Action<bool, string> require, Action<string>? log = null)
    {
        void Require(bool condition, string message) => require(condition, "rinomina in corso: " + message);

        var directory = Path.Combine(Path.GetTempPath(), $"gs_rename_accordi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "rinomina" + ChordSheetFile.Extension);
        var window = Window.GetWindow(this)!;
        var recent = AppSettings.Current.RecentFiles.ToList();
        var previousFocus = FocusManager.GetFocusedElement(window);
        string? RecoveryName() => AutosavePath is { } copy && File.Exists(copy) ? ChordSheetFile.Read(copy).Sections[0].Name : null;
        try
        {
            // 1. Foglio già salvato con la sezione «Strofa».
            NewSheet(confirm: false);
            _editor.Load(new ChordSheet("Rinomina", "", "Do", 0, ImmutableList.Create(
                new SheetSection("Strofa", SheetSectionKind.Lyrics, "[Do]Prima riga\n[Sol]seconda riga"),
                new SheetSection("Giro", SheetSectionKind.Progression, "| Do | Sol |"))));
            await Task.Delay(100);
            Require(SaveTo(path) && !_editor.IsDirty && ChordSheetFile.Read(path).Sections[0].Name == "Strofa",
                "foglio salvato con «Strofa»");
            var undoBefore = _editor.CanUndo;

            // 2. Si riscrive il nome: a metà parola scatta la copia di recupero, poi si finisce.
            // La finestra dell'autotest non si attiva, quindi Focus() non basta: si mette il fuoco
            // logico, quello che genera LostFocus, cioè l'evento che prima registrava il nome.
            var name = _cards[0].Name;
            FocusManager.SetFocusedElement(window, name);
            name.Clear();
            await Type(name, "Ritor");
            log?.Invoke($"rinomina in corso: fuoco logico={FocusManager.GetFocusedElement(window)?.GetType().Name} IsFocused={name.IsFocused} " +
                        $"tastiera={name.IsKeyboardFocused} finestra attiva={window.IsActive} campo=«{name.Text}» foglio=«{_editor.Sheet.Sections[0].Name}»");
            Require(ReferenceEquals(FocusManager.GetFocusedElement(window), name) && _editor.Sheet.Sections[0].Name == "Strofa",
                "il cursore è nel campo e il nome non è ancora nel foglio (il caso da riprodurre)");
            Require(AutosaveNow() && RecoveryName() == "Ritor", "la copia di recupero contiene il nome visibile a metà");
            Require(ReferenceEquals(FocusManager.GetFocusedElement(window), name) && name.Text == "Ritor" && name.CaretIndex == 5,
                "la copia di recupero non sposta il cursore né riscrive il campo");
            await Type(name, "nello");
            Require(AutosaveNow() && RecoveryName() == "Ritornello", "la copia di recupero contiene il nome visibile completo");

            // 3. Ctrl+S con il cursore ancora nel campo.
            await PressKey(Key.S, ModifierKeys.Control);
            Require(!_editor.IsDirty && _currentPath == path && _editor.Sheet.Sections[0].Name == "Ritornello",
                "Ctrl+S salva il nome scritto e il foglio non è più «modificato»");
            Require(ChordSheetFile.Read(path).Sections[0].Name == "Ritornello", "nel file c'è «Ritornello»");
            Require(ReferenceEquals(FocusManager.GetFocusedElement(window), name) && name.Text == "Ritornello",
                "dopo Ctrl+S il cursore resta nel campo con il nome intero");

            // 4. Un solo Ctrl+Z, sempre dal campo, riporta «Strofa» (copie e salvataggio non sono passi in più).
            await PressKey(Key.Z, ModifierKeys.Control);
            Require(_editor.Sheet.Sections[0].Name == "Strofa" && _cards[0].Name.Text == "Strofa" && _editor.CanUndo == undoBefore,
                "un solo Ctrl+Z ripristina «Strofa» nel foglio e nel campo");
            await PressKey(Key.Y, ModifierKeys.Control);
            Require(_editor.Sheet.Sections[0].Name == "Ritornello" && !_editor.IsDirty, "Ctrl+Y rimette «Ritornello»");
            FocusManager.SetFocusedElement(window, _cards[0].Body);
            await Task.Delay(80);
            Require(_editor.Sheet.Sections[0].Name == "Ritornello" && !_editor.IsDirty,
                "uscire dal campo dopo il salvataggio non crea modifiche");

            // 5. Riapertura dal disco.
            NewSheet(confirm: false);
            Require(OpenPath(path, confirm: false) && _editor.Sheet.Sections[0].Name == "Ritornello" &&
                    _cards[0].Name.Text == "Ritornello" && _editor.Sheet.Sections[1].Name == "Giro",
                "riaperto, il foglio mostra «Ritornello»");
        }
        finally
        {
            FocusManager.SetFocusedElement(window, previousFocus);
            _editor.MarkSaved();
            NewSheet(confirm: false);
            AppSettings.Current.RecentFiles = recent;
            AppSettings.Current.Save();
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
        }
    }
}
