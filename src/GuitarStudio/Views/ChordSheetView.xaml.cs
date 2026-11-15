using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GuitarStudio.Editing;
using GuitarStudio.Recovery;
using GuitarStudio.Ui;
using Microsoft.Win32;

namespace GuitarStudio.Views;

/// <summary>
/// Sezione «Accordi»: scrittura e lettura del foglio di accordi. Tutta la logica di modifica
/// sta in <see cref="ChordSheetEditor"/>; qui ci sono solo controlli, file e tastiera.
/// </summary>
public partial class ChordSheetView : UserControl
{
    private sealed record SectionCard(Border Root, TextBox Name, ComboBox Kind, TextBox Body, TextBlock Hint, Button Up, Button Down, Button Remove);

    private readonly ChordSheetEditor _editor = new();
    private readonly List<SectionCard> _cards = new();
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _typingTimer;
    private string? _currentPath;
    private bool _syncing;
    private int _activeSection;
    private (int Section, int Offset)? _chordTarget;
    private bool _readMode;
    private ModifierKeys? _testModifiers;

    public ChordSheetView()
    {
        InitializeComponent();
        ChordPopup.CustomPopupPlacementCallback = PlaceChordPopup;
        _editor.Changed += OnSheetChanged;
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RenderPaper(); };
        // Una pausa nella scrittura chiude il passo di annulla in corso.
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _typingTimer.Tick += (_, _) => { _typingTimer.Stop(); _editor.EndTyping(); };
        CapoCombo.ItemsSource = Enumerable.Range(0, ChordSheetText.MaxCapo + 1).Select(n => n == 0 ? "Nessuno" : $"{n}° tasto").ToList();
        WritePage.SizeChanged += (_, _) => UpdatePreviewVisibility();
        AppSettings.RecentFilesChanged += RebuildRecentMenu;
        RebuildRecentMenu();
        StartAutosave();
        SyncAll();
        SetStatus("Scrivi il testo; Ctrl+K mette un accordo nel punto del cursore. F5 passa alla lettura.");
    }

    internal ChordSheetEditor Editor => _editor;
    public bool IsDirty => _editor.IsDirty;
    private ModifierKeys CurrentModifiers => _testModifiers ?? Keyboard.Modifiers;
    private Window? Owner => Window.GetWindow(this);

    // ================================================================ sincronizzazione modello → controlli

    private bool _typingChange;

    private void OnSheetChanged(bool fromHistory)
    {
        // Solo i caratteri scritti nel riquadro lasciano intatto il campo con il cursore:
        // ogni altra modifica (accordo inserito, annulla, sezioni) riscrive tutti i controlli.
        SyncAll(force: !_typingChange);
    }

    /// <summary>Riporta il foglio nei controlli. Il campo in cui si sta scrivendo non viene riscritto.</summary>
    private void SyncAll(bool force = true)
    {
        _syncing = true;
        try
        {
            var sheet = _editor.Sheet;
            SetIfChanged(TitleBox, sheet.Title, force);
            SetIfChanged(ArtistBox, sheet.Artist, force);
            SetIfChanged(KeyBox, sheet.Key, force);
            if (CapoCombo.SelectedIndex != sheet.Capo) CapoCombo.SelectedIndex = sheet.Capo;

            var structure = sheet.Sections.Count != _cards.Count ||
                            sheet.Sections.Where((s, i) => _cards[i].Kind.SelectedIndex != (int)s.Kind).Any();
            if (structure) RebuildCards();
            for (var i = 0; i < sheet.Sections.Count; i++)
            {
                var section = sheet.Sections[i];
                var card = _cards[i];
                SetIfChanged(card.Name, section.Name, force);
                if (ChordSheetText.Normalize(card.Body.Text) != ChordSheetText.Normalize(section.Body) ||
                    (force && card.Body.Text.Replace("\r\n", "\n") != section.Body))
                {
                    if (!card.Body.IsKeyboardFocusWithin || force)
                    {
                        var caret = card.Body.CaretIndex;
                        card.Body.Text = section.Body;
                        card.Body.CaretIndex = Math.Min(caret, card.Body.Text.Length);
                    }
                }
                card.Up.IsEnabled = i > 0;
                card.Down.IsEnabled = i < sheet.Sections.Count - 1;
            }
        }
        finally { _syncing = false; }

        UndoButton.IsEnabled = _editor.CanUndo;
        SyncTools();
        RedoButton.IsEnabled = _editor.CanRedo;
        UpdateTitle();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private static void SetIfChanged(TextBox box, string value, bool force)
    {
        if (box.Text == value || (box.IsKeyboardFocusWithin && !force)) return;
        box.Text = value;
    }

    private void RebuildCards()
    {
        SectionsHost.Children.Clear();
        _cards.Clear();
        for (var i = 0; i < _editor.Sheet.Sections.Count; i++)
        {
            var card = BuildCard(i, _editor.Sheet.Sections[i]);
            _cards.Add(card);
            SectionsHost.Children.Add(card.Root);
        }
        _activeSection = Math.Clamp(_activeSection, 0, Math.Max(0, _cards.Count - 1));
    }

    private SectionCard BuildCard(int index, SheetSection section)
    {
        Style S(string key) => (Style)FindResource(key);
        var name = new TextBox { Text = section.Name, Width = 190, MaxLength = 40, FontWeight = FontWeights.SemiBold };
        AutomationProperties.SetName(name, "Nome della sezione");
        name.ToolTip = "Nome della sezione: Intro, Strofa, Ritornello, Ponte…";
        var kind = new ComboBox { Width = 170, Margin = new Thickness(8, 0, 0, 0), Focusable = false, VerticalAlignment = VerticalAlignment.Center };
        kind.Items.Add("Testo e accordi");
        kind.Items.Add("Giro di accordi");
        kind.SelectedIndex = (int)section.Kind;
        AutomationProperties.SetName(kind, "Tipo di sezione");
        kind.ToolTip = "Testo con gli accordi sopra le sillabe, oppure giro di soli accordi in battute";
        Button Icon(string glyph, string tip)
        {
            var b = new Button { Style = S("IconButton"), Content = glyph, ToolTip = tip, Width = 34, Height = 32 };
            AutomationProperties.SetName(b, tip);
            return b;
        }
        var up = Icon("", "Sposta la sezione in su");
        var down = Icon("", "Sposta la sezione in giù");
        var remove = Icon("", "Elimina la sezione (Ctrl+Z la rimette)");
        var header = new DockPanel { LastChildFill = false };
        header.Children.Add(name);
        header.Children.Add(kind);
        foreach (var b in new[] { remove, down, up })
        {
            DockPanel.SetDock(b, Dock.Right);
            header.Children.Add(b);
        }

        var body = new TextBox
        {
            Text = section.Body, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap, IsUndoEnabled = false,
            MinHeight = 96, Margin = new Thickness(0, 8, 0, 0), VerticalContentAlignment = VerticalAlignment.Top,
            FontFamily = (FontFamily)FindResource("MonoFont"), FontSize = 15,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetName(body, $"Testo della sezione {section.Name}");
        var hint = new TextBlock
        {
            Foreground = (Brush)FindResource("TextDim"), FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
            Text = HintFor(section.Kind)
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(body);
        stack.Children.Add(hint);
        var root = new Border
        {
            Background = (Brush)FindResource("BgPanel"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10), Child = stack
        };

        name.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitName(index, name); e.Handled = true; } };
        name.LostFocus += (_, _) => CommitName(index, name);
        kind.SelectionChanged += (_, _) =>
        {
            if (_syncing || kind.SelectedIndex < 0) return;
            _activeSection = index;
            if (_editor.SetSectionKind(index, (SheetSectionKind)kind.SelectedIndex))
                SetStatus(kind.SelectedIndex == 1 ? "La sezione è ora un giro di accordi: battute separate da «|»." : "La sezione è ora testo con accordi.");
        };
        up.Click += (_, _) => { _activeSection = _editor.MoveSection(index, -1); FocusBody(_activeSection); };
        down.Click += (_, _) => { _activeSection = _editor.MoveSection(index, +1); FocusBody(_activeSection); };
        remove.Click += (_, _) =>
        {
            var removedName = _editor.Sheet.Sections[index].Name;
            if (_editor.RemoveSection(index)) SetStatus($"Sezione «{removedName}» eliminata. Ctrl+Z la rimette.");
            Focus();
        };
        body.GotFocus += (_, _) => _activeSection = index;
        body.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            _activeSection = index;
            _typingChange = true;
            try { _editor.SetSectionBody(index, body.Text, typing: true); }
            finally { _typingChange = false; }
            _typingTimer.Stop();
            _typingTimer.Start();
        };
        return new SectionCard(root, name, kind, body, hint, up, down, remove);
    }

    private static string HintFor(SheetSectionKind kind) => kind == SheetSectionKind.Lyrics
        ? "Una riga per verso. Ctrl+K mette l'accordo nel punto del cursore, oppure scrivilo tra parentesi: [Do]Nel mezzo del [Sol]cammin."
        : "Una riga per fila di battute: | Do | Sol Lam | Fa |. Più accordi nella stessa battuta separati da spazi.";

    private void CommitName(int index, TextBox box)
    {
        if (_syncing || index >= _editor.Sheet.Sections.Count) return;
        if (box.Text.Trim() != _editor.Sheet.Sections[index].Name) _editor.RenameSection(index, box.Text);
    }

    /// <summary>
    /// Sezione in cui si sta scrivendo: quella con il fuoco (anche solo logico, se la finestra
    /// non è attiva), altrimenti l'ultima usata.
    /// </summary>
    private int ActiveSectionIndex()
    {
        if (Window.GetWindow(this) is { } window && FocusManager.GetFocusedElement(window) is TextBox focused)
            for (var i = 0; i < _cards.Count; i++)
                if (ReferenceEquals(_cards[i].Body, focused) || ReferenceEquals(_cards[i].Name, focused))
                    return _activeSection = i;
        return _activeSection = Math.Clamp(_activeSection, 0, Math.Max(0, _cards.Count - 1));
    }

    private void FocusBody(int index)
    {
        if (index < 0 || index >= _cards.Count) return;
        _activeSection = index; // anche se la finestra non è attiva e il fuoco non si sposta
        var body = _cards[index].Body;
        body.Focus();
        body.BringIntoView();
    }

    // ================================================================ informazioni

    private void InfoField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitInfo();
        e.Handled = true;
    }

    private void InfoField_LostFocus(object sender, RoutedEventArgs e) => CommitInfo();

    private void CapoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || CapoCombo.SelectedIndex < 0) return;
        SetCapo(CapoCombo.SelectedIndex);
    }

    private void CommitInfo()
    {
        if (_syncing) return;
        _editor.SetInfo(TitleBox.Text, ArtistBox.Text, KeyBox.Text, _editor.Sheet.Capo);
    }

    // ================================================================ sezioni

    private void AddLyrics_Click(object sender, RoutedEventArgs e) => AddSection(SheetSectionKind.Lyrics);
    private void AddProgression_Click(object sender, RoutedEventArgs e) => AddSection(SheetSectionKind.Progression);

    private void AddSection(SheetSectionKind kind)
    {
        var after = _cards.Count == 0 ? -1 : ActiveSectionIndex();
        var index = _editor.AddSection(DefaultName(kind, after), kind, after);
        if (index < 0)
        {
            SetStatus($"Il foglio ha già {ChordSheetEditor.MaxSections} sezioni.");
            return;
        }
        _activeSection = index;
        SetStatus($"Nuova sezione «{_editor.Sheet.Sections[index].Name}»: puoi cambiarle nome nel riquadro.");
        Dispatcher.BeginInvoke(() => FocusBody(index), DispatcherPriority.Input);
    }

    /// <summary>Nome proposto: dopo una strofa il ritornello, altrimenti una strofa; i giri partono da Intro.</summary>
    private string DefaultName(SheetSectionKind kind, int after)
    {
        var previous = after >= 0 && after < _editor.Sheet.Sections.Count ? _editor.Sheet.Sections[after].Name : "";
        if (kind == SheetSectionKind.Progression) return _editor.Sheet.Sections.Count == 0 || after < 0 ? "Intro" : "Giro";
        return previous.StartsWith("Strofa", StringComparison.OrdinalIgnoreCase) ? "Ritornello" : "Strofa";
    }

    // ================================================================ accordo nel punto del cursore

    private void InsertChord_Click(object sender, RoutedEventArgs e) => OpenChordEntry();

    /// <summary>Apre il campo dell'accordo accanto al cursore della sezione in cui si sta scrivendo.</summary>
    internal void OpenChordEntry()
    {
        if (_readMode) SetMode(false);
        if (_cards.Count == 0)
        {
            SetStatus("Aggiungi prima una sezione in cui scrivere.");
            return;
        }
        var index = ActiveSectionIndex();
        var body = _cards[index].Body;
        var caret = Math.Min(body.CaretIndex, body.Text.Length);
        // Le posizioni del modello usano \n: il TextBox può contenere \r\n dopo Invio.
        _chordTarget = (index, body.Text[..caret].Replace("\r\n", "\n").Length);
        body.UpdateLayout();
        // Il campo compare accanto al cursore: la riga deve essere visibile nel riquadro di scrittura.
        var rect = body.GetRectFromCharacterIndex(caret);
        if (!rect.IsEmpty && double.IsFinite(rect.Y)) body.BringIntoView(new Rect(0, rect.Y - 8, 1, rect.Height + 140));
        EditorScroll.UpdateLayout();
        rect = body.GetRectFromCharacterIndex(caret);
        ChordPopup.PlacementTarget = body;
        _popupAnchor = new Point(double.IsFinite(rect.X) ? rect.X : 8, double.IsFinite(rect.Bottom) ? rect.Bottom + 4 : 8);
        ChordEntryBox.Text = "";
        ChordEntryHint.Text = "Es. Do, Lam7, Sol/Si · Invio inserisce, Esc annulla";
        ChordEntryHint.Foreground = (Brush)FindResource("TextDim");
        ChordPopup.IsOpen = true;
        Dispatcher.BeginInvoke(() => ChordEntryBox.Focus(), DispatcherPriority.Input);
    }

    private Point _popupAnchor;

    /// <summary>
    /// Il campo va con l'angolo in alto a sinistra sotto il cursore. Placement=Relative non basta:
    /// con l'impostazione «mano destra» di Windows (schermi touch) i popup si aprono a sinistra.
    /// </summary>
    private CustomPopupPlacement[] PlaceChordPopup(Size popupSize, Size targetSize, Point offset)
    {
        var target = ChordPopup.PlacementTarget as FrameworkElement;
        var sx = target is { ActualWidth: > 0 } ? targetSize.Width / target.ActualWidth : 1;
        var sy = target is { ActualHeight: > 0 } ? targetSize.Height / target.ActualHeight : 1;
        return [new CustomPopupPlacement(new Point(_popupAnchor.X * sx, _popupAnchor.Y * sy), PopupPrimaryAxis.Horizontal)];
    }

    private void ChordEntry_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitChordEntry(); e.Handled = true; }
        else if (e.Key == Key.Escape) { ChordPopup.IsOpen = false; e.Handled = true; }
    }

    /// <summary>Inserisce l'accordo scritto nel campo; un nome non valido lascia il campo aperto con il motivo.</summary>
    internal bool CommitChordEntry()
    {
        if (_chordTarget is not { } target) return false;
        if (ChordSheetText.ChordNameProblem(ChordEntryBox.Text) is { } problem)
        {
            ChordEntryHint.Text = problem;
            ChordEntryHint.Foreground = (Brush)FindResource("Danger");
            return false;
        }
        var name = ChordEntryBox.Text.Trim();
        var caret = _editor.InsertChord(target.Section, target.Offset, name);
        _chordTarget = null;
        ChordPopup.IsOpen = false;
        if (caret < 0) return false;
        var body = _cards[target.Section].Body;
        body.Focus();
        body.CaretIndex = Math.Min(caret, body.Text.Length);
        SetStatus($"Accordo {name} inserito. Ctrl+Z lo toglie.");
        return true;
    }

    private void ChordPopup_Closed(object? sender, EventArgs e)
    {
        if (_chordTarget is { } target && target.Section < _cards.Count) _cards[target.Section].Body.Focus();
        _chordTarget = null;
    }

    // ================================================================ scrittura e lettura

    private void Mode_Click(object sender, RoutedEventArgs e) => SetMode((string)((FrameworkElement)sender).Tag == "read");

    internal void SetMode(bool read)
    {
        CommitInfo();
        _readMode = read;
        WriteModeToggle.IsChecked = !read;
        ReadModeToggle.IsChecked = read;
        WritePage.Visibility = read ? Visibility.Collapsed : Visibility.Visible;
        ReadPage.Visibility = read ? Visibility.Visible : Visibility.Collapsed;
        InsertChordButton.IsEnabled = !read;
        ZoomCombo.Visibility = read ? Visibility.Visible : Visibility.Collapsed;
        RenderPaper();
        if (read)
        {
            ReadScroll.ScrollToTop();
            Focus();
            SetStatus("Lettura: frecce, Pag↑/Pag↓ e rotellina per scorrere. F5 torna alla scrittura.");
        }
        else
        {
            FocusBody(_activeSection);
            SetStatus("Scrittura: Ctrl+K mette un accordo nel punto del cursore.");
        }
    }

    private double ReadScale => ZoomCombo.SelectedItem is ComboBoxItem { Tag: string tag } ? double.Parse(tag, System.Globalization.CultureInfo.InvariantCulture) : 1;

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RenderPaper();
    }

    private void RenderPaper()
    {
        if (_readMode)
            ReadScroll.Content = ChordSheetPaper.Build(_editor.Sheet, ReadScale, DisplayChord, AppSettings.Current.ShowChordDiagrams);
        else if (PreviewHost.Visibility == Visibility.Visible)
            PreviewScroll.Content = ChordSheetPaper.Build(_editor.Sheet, 0.75, DisplayChord, AppSettings.Current.ShowChordDiagrams, EditDiagram);
    }

    /// <summary>Nome dell'accordo come compare sulla carta.</summary>
    /// <summary>Nome dell'accordo come compare sulla carta: stessa notazione, con ♯ e ♭.</summary>
    private static string DisplayChord(string name) => ChordName.Parse(name) is { } chord ? chord.Format(chord.Notation, symbols: true) : name;

    private void UpdatePreviewVisibility()
    {
        // Sotto i 900 DIP l'anteprima ruberebbe spazio alla scrittura: resta la lettura (F5).
        var show = WritePage.ActualWidth >= 900;
        PreviewColumn.Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        var was = PreviewHost.Visibility;
        PreviewHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show && was != Visibility.Visible) RenderPaper();
    }

    // ================================================================ tastiera

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = CurrentModifiers;
        var ctrl = mods.HasFlag(ModifierKeys.Control);
        var shift = mods.HasFlag(ModifierKeys.Shift);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (ChordPopup.IsOpen && ChordEntryBox.IsKeyboardFocusWithin) return;
        var handled = true;
        switch (key)
        {
            case Key.K when ctrl && !shift: OpenChordEntry(); break;
            case Key.Up when ctrl && shift: Transpose(+1); break;
            case Key.Down when ctrl && shift: Transpose(-1); break;
            case Key.Z when ctrl && !shift: Undo(); break;
            case Key.Y when ctrl && !shift:
            case Key.Z when ctrl && shift: Redo(); break;
            case Key.N when ctrl && !shift: NewSheet(); break;
            case Key.O when ctrl && !shift: OpenFile(); break;
            case Key.S when ctrl: Save(askPath: shift); break;
            case Key.Enter when ctrl: AddSection(shift ? SheetSectionKind.Progression : SheetSectionKind.Lyrics); break;
            case Key.F5 when mods == ModifierKeys.None: SetMode(!_readMode); break;
            case Key.F when mods == ModifierKeys.Alt: FileMenu_Click(FileMenuButton, new RoutedEventArgs()); break;
            default: handled = false; break;
        }
        if (handled) e.Handled = true;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        CommitInfo();
        if (_editor.Undo()) SetStatus("Annullato.");
    }

    private void Redo()
    {
        if (_editor.Redo()) SetStatus("Ripetuto.");
    }

    // ================================================================ file

    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = FileMenuButton.ContextMenu!;
        menu.PlacementTarget = FileMenuButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Help_Click(object sender, RoutedEventArgs e) => (Owner as MainWindow)?.ShowHelp();
    private void New_Click(object sender, RoutedEventArgs e) => NewSheet();
    private void NewTablature_Click(object sender, RoutedEventArgs e) => (Owner as MainWindow)?.NewTablature();
    private void Open_Click(object sender, RoutedEventArgs e) => OpenFile();
    private void Save_Click(object sender, RoutedEventArgs e) => Save(false);
    private void SaveAs_Click(object sender, RoutedEventArgs e) => Save(true);

    public void NewSheet(bool confirm = true)
    {
        if (confirm && !ConfirmDiscard()) return;
        ClearAutosave();
        _currentPath = null;
        _activeSection = 0;
        _editor.Load(ChordSheet.New());
        SetMode(false);
        SetStatus("Nuovo foglio: scrivi titolo e testo; Ctrl+K mette un accordo nel punto del cursore.");
        Dispatcher.BeginInvoke(() => TitleBox.Focus(), DispatcherPriority.Input);
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Apri un foglio di accordi o una tablatura",
            Filter = "Fogli di accordi (*.accordi)|*.accordi|" +
                     "Tablature (*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.xml;*.musicxml;*.mxl;*.capx)|*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.xml;*.musicxml;*.mxl;*.capx|Tutti i file|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        if (Owner is MainWindow main) main.OpenDocument(dlg.FileName);
        else OpenPath(dlg.FileName);
    }

    /// <summary>Apre un foglio di accordi. Un file non valido lascia aperto il foglio attuale.</summary>
    public bool OpenPath(string path, bool confirm = true)
    {
        if (confirm && !ConfirmDiscard()) return false;
        ChordSheet sheet;
        try { sheet = ChordSheetFile.Read(path); }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            App.Log(ex);
            Dialogs.Warning(Owner, $"Non riesco ad aprire «{Path.GetFileName(path)}».\n\n{ex.Message}");
            return false;
        }
        ClearAutosave();
        _currentPath = path;
        _activeSection = 0;
        _editor.Load(sheet);
        AppSettings.Current.AddRecent(path);
        SetStatus("Aperto: " + Path.GetFileName(path));
        return true;
    }

    internal bool Save(bool askPath)
    {
        CommitInfo();
        var path = _currentPath;
        if (askPath || path == null)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Salva il foglio di accordi",
                FileName = SafeFileName(_editor.Sheet.Title),
                DefaultExt = ChordSheetFile.Extension,
                Filter = "Foglio di accordi (*.accordi)|*.accordi",
                InitialDirectory = path != null ? Path.GetDirectoryName(path) : null
            };
            if (dlg.ShowDialog() != true) return false;
            path = dlg.FileName;
        }
        return SaveTo(path);
    }

    internal bool SaveTo(string path)
    {
        try
        {
            ChordSheetFile.Write(path, _editor.Sheet);
            _currentPath = path;
            _editor.MarkSaved();
            AppSettings.Current.AddRecent(path);
            ClearAutosave();
            UpdateTitle();
            SetStatus("Salvato: " + Path.GetFileName(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Log(ex);
            Dialogs.Warning(Owner, "Salvataggio non riuscito:\n" + ex.Message);
            return false;
        }
    }

    private static string SafeFileName(string title)
    {
        var name = string.IsNullOrWhiteSpace(title) ? "Foglio di accordi" : title.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name + ChordSheetFile.Extension;
    }

    /// <summary>Chiede se salvare le modifiche. Restituisce false se l'utente annulla.</summary>
    public bool ConfirmDiscard()
    {
        CommitInfo();
        if (!_editor.IsDirty) return true;
        var message = string.IsNullOrWhiteSpace(_editor.Sheet.Title)
            ? "Vuoi salvare le modifiche al foglio di accordi?"
            : $"Vuoi salvare le modifiche al foglio «{_editor.Sheet.Title}»?";
        return Dialogs.SaveChanges(Owner, message) switch
        {
            MessageBoxResult.Yes => Save(false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void RebuildRecentMenu() => RecentFilesMenu.Fill(RecentMenu, path =>
    {
        if (Owner is MainWindow main) main.OpenDocument(path);
        else OpenPath(path);
    }, SetStatus);

    // ================================================================ titolo e stato

    /// <summary>Titolo della finestra, solo quando la sezione Accordi è quella visibile.</summary>
    public void UpdateTitle()
    {
        var sheet = _editor.Sheet;
        var name = string.IsNullOrWhiteSpace(sheet.Artist) ? sheet.Title : $"{sheet.Artist} – {sheet.Title}";
        if (string.IsNullOrWhiteSpace(name)) name = "Foglio senza titolo";
        DocTitle.Text = (_currentPath != null ? Path.GetFileName(_currentPath) : "Non salvato") + (_editor.IsDirty ? "  ●" : "");
        if (IsVisible && Owner is { } w) w.Title = $"GuitarStudio — {name}{(_editor.IsDirty ? "  ●" : "")}";
    }

    private void SetStatus(string message) => StatusText.Text = message;
}
