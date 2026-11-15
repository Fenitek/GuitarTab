using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using GuitarStudio.Diagnostics;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private async Task RunLayoutVisualSelfTest(LiveLog log)
    {
        var main = Window.GetWindow(this)!;
        _editor.MarkSaved();
        _editor.Attach(PerformanceScore.Create(Api.Settings, 12, 2), 0);
        _editor.SetTitle("Prova dello spazio partitura");
        _editor.MarkSaved();
        _editor.MoveTo(0, 0, 1);
        Render(); RebuildTracksPanel();
        await VerifyOverlayScrollGuard(log);
        VerifyTransportShortcuts(log);
        var metrics = new List<object>();
        foreach (var (name, width, height) in new[] { ("1920", 1920, 1080), ("intermedia", 1840, 1080), ("minimo", 1760, 960) })
        {
            VisualCapture.SizeAtScale(main, width, height, 2);
            _toastTimer?.Stop(); Toast.Visibility = Visibility.Collapsed;
            StatusText.Text = "Prova dello spazio partitura";
            await VisualCapture.Paint();
            Render(); await VisualCapture.Paint();
            ScrollView!.ScrollToTop(); await VisualCapture.Paint();
            var sv = ScrollView;
            var visible = Api.BoundsLookup!.StaffSystems.Count(s => s.RealBounds.Y >= sv.VerticalOffset - 1 &&
                s.RealBounds.Y + s.RealBounds.H <= sv.VerticalOffset + sv.ViewportHeight + 1);
            log.AppendLine($"{name}: barra={ToolbarLayout.ActualWidth:0.0} px, compatta={_compactToolbar}, viewport={sv.ViewportHeight:0.0} px, sistemi interi={visible}");
            metrics.Add(new { Profile = name, Width = width, Height = height, EffectiveDpi = 192, Compact = _compactToolbar,
                ToolbarWidth = ToolbarLayout.ActualWidth, ScoreViewportHeight = sv.ViewportHeight, FullSystems = visible });
            RequireLayout(_compactToolbar == (name != "1920"), "priorità della barra alla larghezza richiesta");
            if (name == "1920") RequireLayout(visible >= 2, "almeno due sistemi interi al 200% con impostazioni predefinite");
            VerifyToolbarBounds();
            RequireLayout(ScoreAttribution.IsVisible, "attribuzione alphaTab.net fissa e visibile");
            var owner = _compactToolbar ? _overflowFile.Items : FileMenuButton.ContextMenu.Items;
            RequireLayout(owner.Cast<object>().SequenceEqual(_fileCommands), "tutti i comandi File conservano identità e ordine");
            RequireLayout(TracksRow.ActualHeight >= 34 && TracksRow.ActualHeight < 35, "tracce ridotte a una riga");
            _visualCapture!.Save(main, name + "-predefinita");

            FretToggle.IsChecked = true;
            FretToggle_Click(FretToggle, new RoutedEventArgs());
            await VisualCapture.Paint();
            var labels = Fretboard.RenderedLabels;
            RequireLayout(labels.Count > 2 && labels[^1].Fret == 24, "il tasto 24 deve mantenere l'etichetta");
            for (var i = 1; i < labels.Count; i++)
                RequireLayout(labels[i].Bounds.Left >= labels[i - 1].Bounds.Right + 3.9, "etichette del manico distanziate almeno 4 px");
            RequireLayout(labels.All(l => l.Bounds.Left >= 0 && l.Bounds.Right <= Fretboard.ActualWidth), "numeri dentro il manico");
            _visualCapture.Save(main, name + "-manico");
            if (name == "1920") await VerifyPanelResizing(log);
            FretToggle.IsChecked = false;
            FretToggle_Click(FretToggle, new RoutedEventArgs());
            SetPanelHeight(false, 102); await VisualCapture.Paint();
            RequireLayout(TracksHeader.IsVisible, "intestazione delle tracce quando c'è spazio");
            _visualCapture.Save(main, name + "-tracce-estese");
            SetPanelHeight(false, 34);

            More_Click(this, new RoutedEventArgs()); await VisualCapture.Paint();
            VisualCapture.ScalePopup(MoreButton.ContextMenu, 2); await VisualCapture.Paint();
            _visualCapture.Save(main, name + "-altro", MoreButton.ContextMenu);
            MoreButton.ContextMenu.IsOpen = false;
            OpenFileCommands(); await VisualCapture.Paint();
            if (_compactToolbar)
            {
                VisualCapture.ScalePopup(MoreButton.ContextMenu, 2);
                _overflowFile.IsSubmenuOpen = true; await VisualCapture.Paint();
                var popup = MenuPopup(_overflowFile);
                if (popup != null) VisualCapture.ScalePopup(popup, 2);
                await VisualCapture.Paint();
                _visualCapture.Save(main, name + "-file", MoreButton.ContextMenu, popup!);
            }
            else
            {
                VisualCapture.ScalePopup(FileMenuButton.ContextMenu, 2); await VisualCapture.Paint();
                _visualCapture.Save(main, name + "-file", FileMenuButton.ContextMenu);
            }
            FileMenuButton.ContextMenu.IsOpen = MoreButton.ContextMenu.IsOpen = false;
            if (_compactToolbar) await VerifyPracticeMenu(main, name, log);

            QueueTrainer(); await VisualCapture.Paint();
            if (TrainerPopup.Child is FrameworkElement trainer)
            {
                VisualCapture.ScalePopup(trainer, 2); await VisualCapture.Paint();
                _visualCapture.Save(main, name + "-trainer", trainer);
            }
            RequireLayout(TrainerPopup.IsOpen, "speed trainer raggiungibile anche con pulsante nascosto");
            TrainerPopup.IsOpen = false;

            var help = new HelpWindow { Owner = main, ShowActivated = false };
            help.Show();
            VisualCapture.SizeAtScale(help, name == "minimo" ? 1280 : width - 80, name == "minimo" ? 840 : 1000, 2);
            await VisualCapture.Paint();
            _visualCapture.Save(main, name + "-guida", help);
            var tabs = VisualCapture.Descendants<RadioButton>(help).ToArray();
            for (var i = 0; i < tabs.Length; i++)
            {
                tabs[i].IsChecked = true; await VisualCapture.Paint();
                foreach (var text in VisualCapture.Descendants<TextBlock>(help).Where(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Text)))
                {
                    RequireLayout(text.FontSize >= 13, "testi della guida almeno a 13 px");
                    if (text.Text.Length > 70) RequireLayout(text.ActualWidth >= 180, "spazio leggibile per le spiegazioni della guida");
                }
                if (i > 0) _visualCapture.Save(main, name + "-guida-" + (i == 1 ? "effetti" : "audio"), help);
            }
            tabs[0].IsChecked = true; await VisualCapture.Paint();
            var guideScroll = VisualCapture.Descendants<ScrollViewer>(help).First(s => s.IsVisible);
            guideScroll.ScrollToEnd(); await VisualCapture.Paint();
            RequireLayout(guideScroll.VerticalOffset >= guideScroll.ScrollableHeight - 1, "fine della guida raggiungibile");
            _visualCapture.Save(main, name + "-guida-fondo", help);
            help.Close();
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("GS_UI_CAPTURE_DIR")!, "layout-metrics.json"),
            JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        Dialogs.SelfTestPresenter = null;
        _editor.MarkSaved();
        log.AppendLine("foto layout: " + Path.GetFullPath(Environment.GetEnvironmentVariable("GS_UI_CAPTURE_DIR")!));
    }

    private void VerifyToolbarBounds()
    {
        FrameworkElement[] essentials = [PlayButton, StopButton, TransportDisplay, LcdTrack, LcdBar, LcdSig, LcdTempo, TimeText, SpeedSlider, SpeedText, MoreButton];
        var controls = essentials.Concat(VisualCapture.Descendants<ButtonBase>(SecondaryCommands).Where(b => b.IsVisible));
        foreach (var control in controls)
        {
            RequireLayout(control.IsVisible && control.ActualWidth > 0, "controllo di trasporto visibile: " + control.Name);
            var bounds = control.TransformToAncestor(ToolbarLayout).TransformBounds(new Rect(control.RenderSize));
            RequireLayout(bounds.Left >= -0.5 && bounds.Right <= ToolbarLayout.ActualWidth + 0.5 &&
                bounds.Top >= -0.5 && bounds.Bottom <= ToolbarLayout.ActualHeight + 0.5, "nessun controllo tagliato: " + control.Name);
            if (control is ButtonBase)
                RequireLayout(control.ToolTip is string tip && tip.Contains('('), "nome e scorciatoia nel tooltip: " + control.Name);
        }
    }

    private void VerifyTransportShortcuts(LiveLog log)
    {
        void Key(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            _testModifiers = modifiers;
            try
            {
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                RequireLayout(TryHandleToolbarShortcut(args) && args.Handled, "scorciatoia della barra gestita: " + key);
            }
            finally { _testModifiers = null; }
        }
        SetSpeed(100);
        Key(System.Windows.Input.Key.PageUp, ModifierKeys.Control);
        RequireLayout(SpeedSlider.Value == 105 && Api.PlaybackSpeed == 1.05, "scorciatoia velocità più alta");
        Key(System.Windows.Input.Key.PageDown, ModifierKeys.Control);
        RequireLayout(SpeedSlider.Value == 100 && Api.PlaybackSpeed == 1, "scorciatoia velocità più bassa");
        foreach (var (key, state) in new[] {
            (System.Windows.Input.Key.F7, new Func<bool>(() => Api.IsLooping)),
            (System.Windows.Input.Key.F8, () => Api.MetronomeVolume > 0),
            (System.Windows.Input.Key.F9, () => Api.CountInVolume > 0) })
        {
            Key(key); RequireLayout(state(), "scorciatoia di attivazione " + key);
            Key(key); RequireLayout(!state(), "scorciatoia di disattivazione " + key);
        }
        foreach (var (key, modifiers) in new[] {
            (System.Windows.Input.Key.F4, ModifierKeys.Alt),
            (System.Windows.Input.Key.F10, ModifierKeys.Shift) })
        {
            _testModifiers = modifiers;
            try
            {
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                RequireLayout(!TryHandleToolbarShortcut(args) && !args.Handled, "combinazione nativa non intercettata: " + modifiers + "+" + key);
            }
            finally { _testModifiers = null; }
        }
        var inspector = InspectorToggle.IsChecked;
        var revision = _revision;
        var line = _editor.Line;
        var view = ViewCombo.SelectedIndex;
        ViewCombo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, System.Windows.Input.Key.F4)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        RequireLayout(InspectorToggle.IsChecked == inspector, "F4 sul selettore conserva il comportamento nativo");
        foreach (var key in new[] { System.Windows.Input.Key.G, System.Windows.Input.Key.Down })
            ViewCombo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        RequireLayout(_revision == revision && _editor.Line == line, "lettere e frecce sui selettori non modificano le note o il cursore");
        ViewCombo.SelectedIndex = view;
        ViewCombo.IsDropDownOpen = false;
        log.AppendLine("scorciatoie OK: Ctrl+Pag↑/Pag↓, F7/F8/F9, nessun input di sistema inviato");
        log.AppendLine("scorciatoie OK: Alt+F4, Maiusc+F10 e F4 sui selettori lasciati ai controlli nativi");
    }

    private static FrameworkElement? MenuPopup(MenuItem item) =>
        (item.Template.FindName("PART_Popup", item) as Popup)?.Child as FrameworkElement;

    private async Task VerifyPracticeMenu(Window main, string name, LiveLog log)
    {
        More_Click(this, new RoutedEventArgs()); await VisualCapture.Paint();
        VisualCapture.ScalePopup(MoreButton.ContextMenu, 2);
        var practice = MoreButton.ContextMenu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Esercitazione");
        practice.IsSubmenuOpen = true; await VisualCapture.Paint();
        var popup = MenuPopup(practice)!;
        VisualCapture.ScalePopup(popup, 2); await VisualCapture.Paint();
        foreach (var (title, source, current) in new[] {
            ("Loop", LoopToggle, new Func<bool>(() => Api.IsLooping)),
            ("Metronomo", MetronomeToggle, () => Api.MetronomeVolume > 0),
            ("Conteggio iniziale", CountInToggle, () => Api.CountInVolume > 0) })
        {
            var item = practice.Items.OfType<MenuItem>().Single(i => (string)i.Header == title);
            item.IsChecked = true;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            RequireLayout(source.IsChecked == true && current(), "menu e player sincronizzati: " + title);
        }
        practice.IsSubmenuOpen = true; await VisualCapture.Paint();
        _visualCapture!.Save(main, name + "-esercitazione", MoreButton.ContextMenu, popup);
        MoreButton.ContextMenu.IsOpen = false;
        LoopToggle.IsChecked = MetronomeToggle.IsChecked = CountInToggle.IsChecked = false;
        LoopToggle_Click(this, new RoutedEventArgs()); MetronomeToggle_Click(this, new RoutedEventArgs()); CountInToggle_Click(this, new RoutedEventArgs());
        RequireLayout(practice.Items.OfType<MenuItem>().Where(i => i.IsCheckable).All(i => !i.IsChecked), "stato aggiornato anche dal pulsante al menu");
        log.AppendLine(name + ": menu File completo, loop/metronomo/conteggio sincronizzati OK");
    }

    private async Task VerifyPanelResizing(LiveLog log)
    {
        async Task Drag(GridSplitter splitter, double delta)
        {
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(0, delta) { RoutedEvent = Thumb.DragDeltaEvent });
            UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(0, delta, false) { RoutedEvent = Thumb.DragCompletedEvent });
            await VisualCapture.Paint();
        }
        var before = FretRow.ActualHeight;
        await Drag(FretSplitter, -24);
        RequireLayout(FretRow.ActualHeight > before + 10, "il divisore del manico modifica l'altezza");
        before = TracksRow.ActualHeight;
        await Drag(TracksSplitter, -68);
        RequireLayout(TracksRow.ActualHeight > before + 20, "il divisore delle tracce modifica l'altezza");
        var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(AppPaths.DataDirectory, "settings.json")))!;
        RequireLayout(Math.Abs(saved.FretboardHeight - _fretboardHeight) < 1 && Math.Abs(saved.TracksHeight - _tracksHeight) < 1,
            "le due altezze vengono scritte nelle preferenze");
        InitializePanelHeights(saved);
        RequireLayout(Math.Abs(_fretboardHeight - saved.FretboardHeight) < 1 && Math.Abs(_tracksHeight - saved.TracksHeight) < 1, "ripristino delle altezze");
        _fretboardHeight = 162; _tracksHeight = 34; SavePanelHeights(); ApplyPanelHeights();
        await VisualCapture.Paint();
        log.AppendLine("divisori OK: DragStarted/DragDelta/DragCompleted, salvataggio e ripristino da settings.json");
    }
}
