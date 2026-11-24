using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Autotest del foglio di accordi (GS_SELFTEST=1): scrittura, accordo nel punto del cursore con
/// Ctrl+K, annulla/ripeti, sezioni, file, copia di recupero, lettura e foto. Tasti come eventi WPF.
/// </summary>
public partial class ChordSheetView
{
    private async Task PressKey(Key key, ModifierKeys mods = ModifierKeys.None, UIElement? target = null)
    {
        _testModifiers = mods;
        try
        {
            (target ?? this).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
        }
        finally { _testModifiers = null; }
        await Task.Delay(80);
    }

    /// <summary>Scrive carattere per carattere nel riquadro, come la tastiera (un TextChanged per tasto).</summary>
    private async Task Type(TextBox box, string text)
    {
        box.Focus();
        foreach (var c in text)
        {
            var caret = box.CaretIndex;
            box.Text = box.Text.Insert(caret, c.ToString());
            box.CaretIndex = caret + 1;
        }
        await Task.Delay(40);
    }

    private async Task InsertChordWithKeyboard(TextBox body, int caret, string name)
    {
        FocusBody(_cards.FindIndex(c => ReferenceEquals(c.Body, body)));
        body.CaretIndex = caret;
        await PressKey(Key.K, ModifierKeys.Control);
        if (!ChordPopup.IsOpen) throw new InvalidOperationException("Ctrl+K non ha aperto il campo dell'accordo.");
        ChordEntryBox.Text = name;
        ChordEntryBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Enter)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
        await Task.Delay(80);
    }

    internal async Task RunSelfTest(Action<string> log, VisualCapture? capture, bool layout)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Foglio di accordi: " + message);
            log("foglio OK: " + message);
        }

        NewSheet(confirm: false);
        await Task.Delay(150);
        TitleBox.Text = "Canzone di prova"; ArtistBox.Text = "GuitarStudio"; KeyBox.Text = "Sol";
        CommitPendingFields();
        Require(_editor.Sheet is { Title: "Canzone di prova", Key: "Sol" }, "titolo, artista e tonalità nel foglio");

        var body = _cards[0].Body;
        await Type(body, "Nel mezzo del cammin\ndi nostra vita");
        Require(_editor.Sheet.Sections[0].Body == "Nel mezzo del cammin\ndi nostra vita", "testo scritto nella strofa");
        _editor.EndTyping();

        await InsertChordWithKeyboard(body, 0, "Sol");
        await InsertChordWithKeyboard(body, body.Text.IndexOf("cammin", StringComparison.Ordinal), "Re");
        await InsertChordWithKeyboard(body, body.Text.IndexOf("vita", StringComparison.Ordinal), "Mim");
        Require(_editor.Sheet.Sections[0].Body == "[Sol]Nel mezzo del [Re]cammin\ndi nostra [Mim]vita" && body.Text == _editor.Sheet.Sections[0].Body,
            "Ctrl+K inserisce gli accordi nel punto del cursore");
        Require(body.CaretIndex == body.Text.IndexOf("vita", StringComparison.Ordinal), "il cursore resta dopo l'accordo inserito");

        ChordEntryBox.Text = "";
        await InsertChordWithKeyboard(body, 0, "Do Re");
        Require(ChordPopup.IsOpen && ChordEntryHint.Text.Contains("spazi", StringComparison.Ordinal), "un nome con spazi viene rifiutato nel campo");
        ChordPopup.IsOpen = false;

        await PressKey(Key.Z, ModifierKeys.Control);
        Require(_editor.Sheet.Sections[0].Body == "[Sol]Nel mezzo del [Re]cammin\ndi nostra vita" && body.Text == _editor.Sheet.Sections[0].Body,
            "Ctrl+Z toglie l'ultimo accordo anche dal riquadro");
        await PressKey(Key.Y, ModifierKeys.Control);
        Require(_editor.Sheet.Sections[0].Body.EndsWith("[Mim]vita", StringComparison.Ordinal), "Ctrl+Y lo rimette");
        for (var i = 0; i < 3; i++) await PressKey(Key.Z, ModifierKeys.Control);
        Require(_editor.Sheet.Sections[0].Body == "Nel mezzo del cammin\ndi nostra vita", "tre Ctrl+Z tolgono i tre accordi");
        await PressKey(Key.Z, ModifierKeys.Control);
        Require(_editor.Sheet.Sections[0].Body == "", "la scrittura continua è un solo passo di annulla");
        for (var i = 0; i < 4; i++) await PressKey(Key.Y, ModifierKeys.Control);
        Require(_editor.Sheet.Sections[0].Body == "[Sol]Nel mezzo del [Re]cammin\ndi nostra [Mim]vita", "Ctrl+Y rifà tutto");

        FocusBody(0);
        await PressKey(Key.Enter, ModifierKeys.Control | ModifierKeys.Shift);
        Require(_editor.Sheet.Sections.Count == 2 && _editor.Sheet.Sections[1] is { Kind: SheetSectionKind.Progression }, "Ctrl+Maiusc+Invio aggiunge un giro");
        _cards[1].Name.Text = "Intro";
        CommitName(1, _cards[1].Name);
        await Type(_cards[1].Body, "| Sol | Re | Mim Do |");
        _editor.EndTyping();
        _cards[1].Up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Require(_editor.Sheet.Sections[0] is { Name: "Intro", Kind: SheetSectionKind.Progression } && _cards[0].Name.Text == "Intro",
            "il giro rinominato Intro sale in cima");
        await InsertChordWithKeyboard(_cards[0].Body, _cards[0].Body.Text.IndexOf("| Re", StringComparison.Ordinal), "Sol7");
        Require(_editor.Sheet.Sections[0].Body == "| Sol Sol7 | Re | Mim Do |", "Ctrl+K nel giro aggiunge l'accordo alla battuta");
        FocusBody(1);
        await PressKey(Key.Enter, ModifierKeys.Control);
        Require(_editor.Sheet.Sections.Count == 3 && _editor.Sheet.Sections[2] is { Name: "Ritornello", Kind: SheetSectionKind.Lyrics },
            "Ctrl+Invio dopo la strofa propone il ritornello");
        await Type(_cards[2].Body, "Mi ritrovai per una selva oscura");
        _editor.EndTyping();
        await InsertChordWithKeyboard(_cards[2].Body, 0, "Do");
        await InsertChordWithKeyboard(_cards[2].Body, _cards[2].Body.Text.IndexOf("selva", StringComparison.Ordinal), "Re");

        // File: salva, riapri, e i recenti elencano fogli e tablature.
        var directory = Path.Combine(Path.GetTempPath(), "gs_selftest_accordi");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Canzone di prova" + ChordSheetFile.Extension);
        var tabPath = Path.Combine(directory, "Tablatura di prova.gp");
        File.WriteAllBytes(tabPath, Array.Empty<byte>());
        AppSettings.Current.AddRecent(tabPath);
        var expected = _editor.Sheet;
        Require(SaveTo(path) && !_editor.IsDirty && DocTitle.Text.StartsWith("Canzone di prova.accordi", StringComparison.Ordinal), "salvataggio .accordi");
        var json = File.ReadAllText(path);
        Require(json.Contains("\"tipo\": \"giro\"", StringComparison.Ordinal) && json.Contains("[Sol]Nel mezzo del [Re]cammin", StringComparison.Ordinal),
            "il file è JSON leggibile con le righe come le hai scritte");
        NewSheet(confirm: false);
        Require(OpenPath(path, confirm: false) && _editor.Sheet.SameContent(expected) && _cards.Count == 3, "riapertura identica");
        var recent = RecentMenu.Items.OfType<MenuItem>().ToList();
        Require(recent.Count >= 2 && (string)recent[0].Header == Path.GetFileName(path) && recent[0].InputGestureText == "foglio di accordi" &&
                recent.Any(i => (string)i.Header == "Tablatura di prova.gp" && i.InputGestureText == "tablatura"),
            "file recenti con entrambi i tipi");

        // Copia di recupero: si crea con modifiche non salvate, sparisce al salvataggio.
        FocusBody(1);
        await Type(_cards[1].Body, "\n");
        Require(_editor.IsDirty && AutosaveNow() && AutosavePath is { } copy && File.Exists(copy) &&
                ChordSheetFile.Read(copy).Sections.Count == 3, "copia di recupero .accordi scritta");
        Require(SaveTo(path) && !File.Exists(AutosavePath), "il salvataggio elimina la copia di recupero");

        // Lettura: carta chiara, accordi sopra le sillabe, caratteri grandi.
        await PressKey(Key.F5);
        Require(ReadPage.IsVisible && !WritePage.IsVisible, "F5 passa alla lettura");
        await VisualCapture.Paint();
        var texts = VisualCapture.Descendants<TextBlock>((DependencyObject)ReadScroll.Content).ToList();
        var chord = texts.First(t => t.Text == "Re" && t.Parent is StackPanel { Children.Count: 2 } p && ReferenceEquals(p.Children[0], t));
        var syllable = (TextBlock)((StackPanel)chord.Parent).Children[1];
        var chordBox = chord.TransformToAncestor(ReadScroll).TransformBounds(new Rect(chord.RenderSize));
        var syllableBox = syllable.TransformToAncestor(ReadScroll).TransformBounds(new Rect(syllable.RenderSize));
        Require(syllable.Text.StartsWith("cammin", StringComparison.Ordinal) && chordBox.Bottom <= syllableBox.Top + 1 &&
                Math.Abs(chordBox.Left - syllableBox.Left) < 1, "in lettura l'accordo sta sopra la sua sillaba");
        Require(syllable.FontSize >= 22 && chord.FontSize >= 20 && texts.All(t => t.FontSize >= 13), "caratteri grandi, nessuno sotto 13 px");
        Require(ReferenceEquals(chord.Foreground, FindResource("SheetChord")) && ReferenceEquals(ReadPage.Background, FindResource("Paper")), "colori dal tema su carta chiara");
        await PressKey(Key.F5);
        Require(WritePage.IsVisible, "F5 torna alla scrittura");
        await VerifyTools(Require, directory);

        if (capture != null) await CaptureSheet(capture, layout, log);
        await VerifySafeSave(Require);
        await VerifyRenameWhileTyping(Require, log);
        _editor.MarkSaved();
        AppSettings.Current.RemoveRecent(path);
        AppSettings.Current.RemoveRecent(tabPath);
        Directory.Delete(directory, true);
    }

    private async Task CaptureSheet(VisualCapture capture, bool layout, Action<string> log)
    {
        var main = Window.GetWindow(this)!;
        var profiles = layout ? new[] { "1920-200-" } : new[] { "1920-", "minimo-" };
        foreach (var prefix in profiles)
        {
            if (!layout) VisualCapture.Size(main, prefix == "minimo-");
            else VisualCapture.SizeAtScale(main, 1920, 1080, 2);
            SetMode(false);
            FocusBody(1);
            _cards[1].Body.CaretIndex = _cards[1].Body.Text.IndexOf("vita", StringComparison.Ordinal);
            EditorScroll.ScrollToTop();
            SetStatus("Scrittura: Ctrl+K mette un accordo nel punto del cursore.");
            await VisualCapture.Paint();
            capture.Save(main, prefix + "foglio-scrittura");
            OpenChordEntry();
            ChordEntryBox.Text = "Lam";
            await VisualCapture.Paint();
            var anchor = _cards[1].Body.PointToScreen(_popupAnchor);
            var placed = ChordPopup.Child.PointToScreen(new Point(0, 0));
            if (Math.Abs(anchor.X - placed.X) > 4 || Math.Abs(anchor.Y - placed.Y) > 4)
                throw new InvalidOperationException($"Campo dell'accordo lontano dal cursore: {placed} invece di {anchor}.");
            await VisualCapture.Paint();
            capture.Save(main, prefix + "foglio-accordo-al-cursore", ChordPopup.Child);
            ChordPopup.IsOpen = false;
            SetMode(true);
            await VisualCapture.Paint();
            if (ReadScroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                throw new InvalidOperationException("Barra orizzontale nella lettura del foglio.");
            capture.Save(main, prefix + "foglio-lettura");
            SetMode(false);
            await VisualCapture.Paint();
            CaptureDiagramDialog(capture, main, prefix);
            log($"foto foglio {prefix}: scrittura, accordo al cursore, lettura (anteprima {(PreviewHost.IsVisible ? "visibile" : "nascosta")})");
        }
        SetMode(false);
    }
}
