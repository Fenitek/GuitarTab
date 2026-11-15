using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private bool _panelHeightsReady;
    private bool _applyingPanelHeights;
    private double _fretboardHeight = 162;
    private double _tracksHeight = 34;

    private static double ValidHeight(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    private void InitializePanelHeights(AppSettings settings)
    {
        _fretboardHeight = Math.Clamp(ValidHeight(settings.FretboardHeight, 162), 149, 600);
        _tracksHeight = Math.Clamp(ValidHeight(settings.TracksHeight, 34), 34, 600);
        _panelHeightsReady = true;
        ApplyPanelHeights();
    }

    private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyPanelHeights();

    private void ApplyPanelHeights()
    {
        if (!_panelHeightsReady || _applyingPanelHeights || WorkspaceGrid.ActualHeight <= 0) return;
        _applyingPanelHeights = true;
        try
        {
            var fret = FretPanel.Visibility == Visibility.Visible;
            var tracks = TracksPanel.Visibility == Visibility.Visible;
            var fretMin = Math.Max(149, Fretboard.MinHeight + 12);
            var available = Math.Max(140, WorkspaceGrid.ActualHeight - TransportBar.ActualHeight - StatusBar.ActualHeight -
                (tracks ? 6 : 0) - (fret ? 6 : 0));
            var tracksMax = Math.Max(34, available - 140 - (fret ? fretMin : 0));
            TracksRow.MinHeight = tracks ? 34 : 0;
            TracksRow.MaxHeight = tracks ? tracksMax : 0;
            TracksRow.Height = new GridLength(tracks ? Math.Clamp(_tracksHeight, 34, tracksMax) : 0);
            TracksHeader.Visibility = TracksRow.Height.Value >= 62 ? Visibility.Visible : Visibility.Collapsed;
            TracksSplitter.Visibility = tracks ? Visibility.Visible : Visibility.Collapsed;
            TracksDividerRow.Height = new GridLength(tracks ? 6 : 0);
            var fretMax = Math.Max(fretMin, available - 140 - TracksRow.Height.Value);
            FretRow.MinHeight = fret ? fretMin : 0;
            FretRow.MaxHeight = fret ? fretMax : 0;
            FretRow.Height = new GridLength(fret ? Math.Clamp(_fretboardHeight, fretMin, fretMax) : 0);
            FretSplitter.Visibility = fret ? Visibility.Visible : Visibility.Collapsed;
            FretDividerRow.Height = new GridLength(fret ? 6 : 0);
            BodyRow.MinHeight = 140 + (fret ? fretMin + 6 : 0);
            FitSidePanels();
        }
        finally { _applyingPanelHeights = false; }
    }

    private void FitSidePanels()
    {
        var palette = PalettePanel.IsVisible;
        var inspector = InspectorPanel.IsVisible;
        var remaining = Math.Max(0, ActualWidth - 300);
        var pw = palette ? Math.Clamp(_paletteWidth, PaletteMinWidth, PaletteColumn.MaxWidth) : 0;
        var iw = inspector ? Math.Clamp(_inspectorWidth, InspectorMinWidth, InspectorColumn.MaxWidth) : 0;
        var excess = Math.Max(0, pw + iw - remaining);
        if (inspector) { var reduction = Math.Min(excess, iw - InspectorMinWidth); iw -= reduction; excess -= reduction; }
        if (palette) pw = Math.Max(PaletteMinWidth, pw - excess);
        if (palette) PaletteColumn.Width = new GridLength(pw);
        if (inspector) InspectorColumn.Width = new GridLength(iw);
    }

    private void HeightSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender == FretSplitter) _fretboardHeight = FretRow.ActualHeight;
        else _tracksHeight = TracksRow.ActualHeight;
        ApplyPanelHeights();
        SavePanelHeights();
    }

    private void HeightSplitter_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
            Dispatcher.BeginInvoke(() => HeightSplitter_DragCompleted(sender, new DragCompletedEventArgs(0, 0, false)));
    }

    private void SideSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        StoreLayout(AppSettings.Current);
        AppSettings.Current.Save();
        FitSidePanels();
    }

    private void SetPanelHeight(bool fret, double height)
    {
        if (fret) _fretboardHeight = Math.Clamp(height, 149, 600);
        else _tracksHeight = Math.Clamp(height, 34, 600);
        ApplyPanelHeights();
        SavePanelHeights();
    }

    private void SavePanelHeights()
    {
        AppSettings.Current.FretboardHeight = _fretboardHeight;
        AppSettings.Current.TracksHeight = _tracksHeight;
        AppSettings.Current.Save();
    }
}
