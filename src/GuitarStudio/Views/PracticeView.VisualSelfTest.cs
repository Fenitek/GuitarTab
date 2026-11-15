using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GuitarStudio.Diagnostics;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private VisualCapture? _visualCapture;

    private void ConfigureVisualSelfTest()
    {
        if (Environment.GetEnvironmentVariable("GS_SELFTEST") != "1" ||
            Environment.GetEnvironmentVariable("GS_SELFTEST_UI") != "1" ||
            Environment.GetEnvironmentVariable("GS_UI_CAPTURE_DIR") is not { Length: > 0 } path) return;
        if (Environment.GetEnvironmentVariable("GS_SELFTEST_DATA_DIR") is not { Length: > 0 })
            throw new InvalidOperationException("Le foto dell'autotest richiedono GS_SELFTEST_DATA_DIR per isolare i dati di prova.");
        _visualCapture = new VisualCapture(Path.GetFullPath(path));
        Dialogs.SelfTestPresenter = window =>
        {
            var main = Window.GetWindow(this)!;
            window.ShowActivated = false;
            window.Show();
            foreach (var minimum in new[] { false, true })
            {
                VisualCapture.Size(main, minimum);
                window.Left = main.Left + (main.ActualWidth - window.ActualWidth) / 2;
                window.Top = main.Top + (main.ActualHeight - window.ActualHeight) / 2;
                VisualCapture.PaintSynchronously();
                _visualCapture.Save(main, (minimum ? "minimo-" : "1920-") + "recupera", window);
            }
            window.Close();
            // Solo fixture sintetiche nella cartella isolata dell'autotest.
            return MessageBoxResult.Yes;
        };
    }

    private async Task RunVisualSelfTest(LiveLog log)
    {
        if (Environment.GetEnvironmentVariable("GS_SELFTEST_LAYOUT") == "1")
        {
            await RunLayoutVisualSelfTest(log);
            return;
        }
        var main = Window.GetWindow(this)!;
        await VisualCapture.Paint();
        // Conserva anche lo stato iniziale: mostra la precedenza tra recupero e file richiesto.
        _visualCapture!.Save(main, "avvio-recuperato");
        log.AppendLine("avvio: " + StatusText.Text);
        if (main.Icon is not System.Windows.Media.Imaging.BitmapFrame { Decoder.Frames.Count: >= 5 } icon)
            throw new InvalidOperationException("La finestra principale non ha l'icona dell'applicazione.");
        log.AppendLine($"icona finestra: {string.Join(",", icon.Decoder.Frames.Select(f => f.PixelWidth))} px");
        if (App.StartupFile is { } requested)
        {
            if (_currentPath == null)
            {
                if (!_editor.IsDirty || !StatusText.Text.Contains(Path.GetFileName(requested), StringComparison.Ordinal) ||
                    !StatusText.Text.Contains("non è stato aperto", StringComparison.Ordinal) || !Toast.IsVisible)
                    throw new InvalidOperationException("Manca l'avviso sul file richiesto dopo il recupero.");
                if (ToastText.ActualWidth + ToastIcon.ActualWidth + ToastIcon.Margin.Right > Toast.ActualWidth - Toast.Padding.Left - Toast.Padding.Right)
                    throw new InvalidOperationException("Il testo del recupero supera la larghezza del toast.");
                log.AppendLine("recupero + riga di comando: copia non salvata, toast con nome file ignorato OK");
            }
            else
            {
                if (!string.Equals(Path.GetFullPath(requested), Path.GetFullPath(_currentPath), StringComparison.OrdinalIgnoreCase) || _editor.IsDirty)
                    throw new InvalidOperationException("Il file richiesto senza recupero non è stato aperto correttamente.");
                log.AppendLine("riga di comando senza recupero: file richiesto aperto OK");
            }
        }
        _editor.MarkSaved();
        _editor.Attach(PerformanceScore.Create(Api.Settings, 12, 2), 0);
        _editor.SetTitle("Controllo visivo");
        _editor.MarkSaved();
        _editor.MoveTo(0, 0, 1);
        FretToggle.IsChecked = true;
        FretPanel.Visibility = Visibility.Visible;
        UpdateFretboard();
        SetEditMode(true);
        Render(); RebuildTracksPanel();
        foreach (var minimum in new[] { false, true })
        {
            var prefix = minimum ? "minimo-" : "1920-";
            VisualCapture.Size(main, minimum);
            PaletteColumn.Width = new GridLength(226);
            InspectorColumn.Width = new GridLength(290);
            ShowInspector(TabTrack);
            _toastTimer?.Stop(); Toast.BeginAnimation(OpacityProperty, null); Toast.Visibility = Visibility.Collapsed;
            StatusText.Text = "Controllo visivo: note, selezione e tracce";
            await VisualCapture.Paint();
            _visualCapture.Save(main, prefix + "principale");
            await CaptureNotationScene(main, prefix, "dinamiche-abbellimenti", PrepareNotationScene);
            ShowInspector(TabTrack);

            async Task Combos(DependencyObject root, string suffix, Window? child = null)
            {
                foreach (var combo in VisualCapture.Descendants<ComboBox>(root).Where(c => c.IsVisible && c.IsEnabled).ToArray())
                {
                    combo.IsDropDownOpen = true;
                    await VisualCapture.Paint();
                    var popup = combo.Template.FindName("PART_Popup", combo) as Popup;
                    var overlays = new List<Visual>();
                    if (child != null) overlays.Add(child);
                    if (popup?.Child is Visual visual) overlays.Add(visual);
                    _visualCapture.Save(main, prefix + suffix + combo.Name, overlays.ToArray());
                    combo.IsDropDownOpen = false;
                    await VisualCapture.Paint();
                }
            }
            await Combos(this, "combo-");
            ShowInspector(TabBar); await VisualCapture.Paint();
            await Combos(BarPage, "combo-battuta-");
            ShowInspector(TabTrack);

            var menu = FileMenuButton.ContextMenu!;
            menu.PlacementTarget = FileMenuButton; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
            await VisualCapture.Paint();
            _visualCapture.Save(main, prefix + "menu-file", menu);
            menu.IsOpen = false;

            var help = new HelpWindow { Owner = main, ShowActivated = false };
            help.Width = minimum ? help.MinWidth : Math.Min(help.Width, main.Width - 32);
            help.Height = minimum ? help.MinHeight : Math.Min(help.Height, main.Height - 40);
            help.Show(); await VisualCapture.Paint();
            _visualCapture.Save(main, prefix + "guida", help);
            help.Close();

            var track = new TrackPropertiesWindow { Owner = main, ShowActivated = false };
            track.Show(); await VisualCapture.Paint();
            _visualCapture.Save(main, prefix + "nuova-traccia", track);
            await Combos(track, "nuova-traccia-", track);
            track.Close();

            ShowToast("Lo slide parte da questa nota e arriva alla nota successiva sulla stessa corda, che qui manca.\nScrivi prima la nota di arrivo.", ToastKind.Warning);
            await VisualCapture.Paint();
            _visualCapture.Save(main, prefix + "avviso");
            _toastTimer?.Stop(); Toast.Visibility = Visibility.Collapsed;
        }
        Dialogs.SelfTestPresenter = null;
        _editor.MarkSaved();
        log.AppendLine("foto: " + Environment.GetEnvironmentVariable("GS_UI_CAPTURE_DIR"));
    }
}
