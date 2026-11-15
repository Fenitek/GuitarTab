using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Automation;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private const double FullToolbarMinWidth = 820;
    private bool _toolbarReady;
    private bool _toolbarModeApplied;
    private bool _compactToolbar;
    private MenuItem _overflowFile = null!;
    private object[] _fileCommands = [];
    private readonly List<UIElement> _overflowCommands = [];

    private void InitializeAdaptiveToolbar()
    {
        var menu = MoreButton.ContextMenu;
        _fileCommands = FileMenuButton.ContextMenu.Items.Cast<object>().ToArray();
        _overflowFile = new MenuItem { Header = "File", InputGestureText = "Alt+F" };
        menu.Items.Add(_overflowFile); _overflowCommands.Add(_overflowFile);
        var history = new MenuItem { Header = "Modifiche" };
        Command(history.Items, "Annulla", "Ctrl+Z", () => _editor.Undo()).SetBinding(IsEnabledProperty,
            new Binding(nameof(IsEnabled)) { Source = UndoButton });
        Command(history.Items, "Ripeti", "Ctrl+Y", () => _editor.Redo()).SetBinding(IsEnabledProperty,
            new Binding(nameof(IsEnabled)) { Source = RedoButton });
        menu.Items.Add(history); _overflowCommands.Add(history);
        var navigation = new MenuItem { Header = "Spostamento" };
        Command(navigation.Items, "All'inizio", "Ctrl+Home", () => GoStart_Click(this, new RoutedEventArgs()));
        Command(navigation.Items, "Battuta precedente", "Ctrl+←", () => JumpBar(-1));
        Command(navigation.Items, "Battuta successiva", "Ctrl+→", () => JumpBar(1));
        menu.Items.Add(navigation); _overflowCommands.Add(navigation);
        var practice = new MenuItem { Header = "Esercitazione" };
        ToggleCommand(practice.Items, "Loop", "F7", LoopToggle, () => LoopToggle_Click(this, new RoutedEventArgs()));
        ToggleCommand(practice.Items, "Metronomo", "F8", MetronomeToggle, () => MetronomeToggle_Click(this, new RoutedEventArgs()));
        ToggleCommand(practice.Items, "Conteggio iniziale", "F9", CountInToggle, () => CountInToggle_Click(this, new RoutedEventArgs()));
        Command(practice.Items, "Speed trainer…", "Ctrl+F10", QueueTrainer);
        menu.Items.Add(practice); _overflowCommands.Add(practice);

        var panels = new MenuItem { Header = "Pannelli" };
        ToggleCommand(panels.Items, "Tavolozza di modifica", "F2", PaletteToggle, () => SetEditMode(PaletteToggle.IsChecked == true));
        ToggleCommand(panels.Items, "Tracce", "F3", TracksToggle, () => PanelToggle_Click(TracksToggle, new RoutedEventArgs()));
        ToggleCommand(panels.Items, "Proprietà", "F4", InspectorToggle, () => PanelToggle_Click(InspectorToggle, new RoutedEventArgs()));
        ToggleCommand(panels.Items, "Manico", "F6", FretToggle, () => FretToggle_Click(FretToggle, new RoutedEventArgs()));
        menu.Items.Add(panels);
        var tracks = new MenuItem { Header = "Tracce" };
        Command(tracks.Items, "Aggiungi una traccia…", "Ctrl+Maiusc+N", () => AddTrack_Click(this, new RoutedEventArgs()));
        Command(tracks.Items, "Elimina traccia…", "Ctrl+Maiusc+D", () => DeleteTrack_Click(this, new RoutedEventArgs()));
        menu.Items.Add(tracks);
        var space = new MenuItem { Header = "Spazio partitura" };
        Command(space.Items, "Tracce: una riga", "", () => SetPanelHeight(false, 34));
        Command(space.Items, "Tracce: più alte", "", () => SetPanelHeight(false, _tracksHeight + 34));
        Command(space.Items, "Tracce: più basse", "", () => SetPanelHeight(false, _tracksHeight - 34));
        space.Items.Add(new Separator());
        Command(space.Items, "Manico: più alto", "", () => SetPanelHeight(true, _fretboardHeight + 24));
        Command(space.Items, "Manico: più basso", "", () => SetPanelHeight(true, _fretboardHeight - 24));
        Command(space.Items, "Ripristina altezze predefinite", "", () => { _fretboardHeight = 162; _tracksHeight = 34; SavePanelHeights(); ApplyPanelHeights(); });
        menu.Items.Add(space);
        Command(menu.Items, "Guida rapida", "F1", () => Help_Click(this, new RoutedEventArgs()));
        _toolbarReady = true;
        foreach (var button in SecondaryCommands.Children.OfType<ButtonBase>())
            AutomationProperties.SetName(button, ((string)button.ToolTip).Split('(')[0].Trim());
        UpdateToolbarMode();
    }

    private static MenuItem Command(ItemCollection items, string text, string shortcut, Action action)
    {
        var item = new MenuItem { Header = text, InputGestureText = shortcut };
        item.Click += (_, e) => { if (!ReferenceEquals(e.OriginalSource, item)) return; action(); e.Handled = true; };
        items.Add(item);
        return item;
    }

    private static void ToggleCommand(ItemCollection items, string text, string shortcut, ToggleButton source, Action action)
    {
        var item = Command(items, text, shortcut, action);
        item.IsCheckable = true;
        item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = source, Mode = BindingMode.TwoWay });
    }

    private void Toolbar_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateToolbarMode();

    private void UpdateToolbarMode()
    {
        if (!_toolbarReady || ToolbarLayout.ActualWidth <= 0) return;
        var compact = ToolbarLayout.ActualWidth < FullToolbarMinWidth;
        if (_toolbarModeApplied && compact == _compactToolbar) return;
        _compactToolbar = compact;
        _toolbarModeApplied = true;
        FileMenuButton.ContextMenu.IsOpen = MoreButton.ContextMenu.IsOpen = false;
        // Gli stessi MenuItem cambiano contenitore: recenti e SoundFont conservano stato e handler.
        FileMenuButton.ContextMenu.Items.Clear(); _overflowFile.Items.Clear();
        var destination = compact ? _overflowFile.Items : FileMenuButton.ContextMenu.Items;
        foreach (var command in _fileCommands) destination.Add(command);
        SecondaryCommands.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in _overflowCommands) item.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.ContextMenu.PlacementTarget = MoreButton;
        MoreButton.ContextMenu.Placement = PlacementMode.Bottom;
        MoreButton.ContextMenu.IsOpen = true;
    }

    private void OpenFileCommands()
    {
        if (_compactToolbar) { More_Click(this, new RoutedEventArgs()); _overflowFile.IsSubmenuOpen = true; }
        else FileMenu_Click(this, new RoutedEventArgs());
    }

    private void QueueTrainer() => Dispatcher.BeginInvoke(() => TrainerToggle_Click(this, new RoutedEventArgs()));

    private bool TryHandleToolbarShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = CurrentModifiers.HasFlag(ModifierKeys.Control);
        var alt = CurrentModifiers.HasFlag(ModifierKeys.Alt);
        var shift = CurrentModifiers.HasFlag(ModifierKeys.Shift);
        if (key == Key.F4 && e.OriginalSource is ComboBox or ComboBoxItem) return false;
        var plain = !ctrl && !alt && !shift;
        Action? action = key switch
        {
            Key.F2 when plain => () => SetEditMode(!EditMode),
            Key.F3 when plain => () => { TracksToggle.IsChecked = TracksToggle.IsChecked != true; PanelToggle_Click(TracksToggle, e); },
            Key.F4 when plain => () => { InspectorToggle.IsChecked = InspectorToggle.IsChecked != true; PanelToggle_Click(InspectorToggle, e); },
            Key.F6 when plain => () => { FretToggle.IsChecked = FretToggle.IsChecked != true; FretToggle_Click(FretToggle, e); },
            Key.F7 when plain => () => { LoopToggle.IsChecked = LoopToggle.IsChecked != true; LoopToggle_Click(LoopToggle, e); },
            Key.F8 when plain => () => { MetronomeToggle.IsChecked = MetronomeToggle.IsChecked != true; MetronomeToggle_Click(MetronomeToggle, e); },
            Key.F9 when plain => () => { CountInToggle.IsChecked = CountInToggle.IsChecked != true; CountInToggle_Click(CountInToggle, e); },
            Key.F10 when ctrl && !alt && !shift => QueueTrainer,
            Key.F10 when plain => () => More_Click(this, e),
            Key.F when alt && !ctrl && !shift => OpenFileCommands,
            Key.Home when ctrl && !alt && !shift => () => GoStart_Click(this, e),
            Key.PageUp when ctrl && !alt && !shift => () => SetSpeed((int)SpeedSlider.Value + 5),
            Key.PageDown when ctrl && !alt && !shift => () => SetSpeed((int)SpeedSlider.Value - 5),
            Key.N when ctrl && shift && !alt => () => AddTrack_Click(this, e),
            Key.D when ctrl && shift && !alt => () => DeleteTrack_Click(this, e),
            _ => null
        };
        if (action == null) return false;
        action(); e.Handled = true;
        return true;
    }
}
