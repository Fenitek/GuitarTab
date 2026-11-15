using System.Windows;
using AlphaTab;
using AlphaTab.Importer;
using GuitarStudio.Diagnostics;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private async Task VerifyTabRhythmVisual(Window main, LiveLog log)
    {
        var original = _editor.Score!;
        var importer = new AlphaTexImporter();
        importer.InitFromString("\\title \"Durate nella tablatura\" . " +
            string.Join(" | ", Enumerable.Repeat(":4 3.1{d} :8 5.1 :8 7.2 8.2 :16 5.3 7.3 5.4 7.4", 12)), Api.Settings);
        _editor.Attach(importer.ReadScore(), 0);
        Render(); await VisualCapture.Paint();
        ScrollView!.ScrollToTop(); await VisualCapture.Paint();
        RequireLayout(Api.Settings.Notation.RhythmMode == TabRhythmMode.ShowWithBars, "ritmo esplicito sotto Solo tab");
        var sv = ScrollView;
        var visible = Api.BoundsLookup!.StaffSystems.Count(s => s.RealBounds.Y >= sv.VerticalOffset - 1 &&
            s.RealBounds.Y + s.RealBounds.H <= sv.VerticalOffset + sv.ViewportHeight + 1);
        RequireLayout(visible >= 2, "due sistemi interi con durate e punto al 200%");
        _visualCapture!.Save(main, "1920-solo-tab-ritmo");
        log.AppendLine($"ritmo Solo tab: {visible} sistemi interi; viewport={sv.ViewportHeight:0.0} DIP");
        ViewCombo.SelectedIndex = 0; await VisualCapture.Paint();
        RequireLayout(Api.Settings.Notation.RhythmMode == TabRhythmMode.Hidden, "nessun ritmo duplicato in Tab + spartito");
        _visualCapture.Save(main, "1920-tab-spartito-ritmo");
        ViewCombo.SelectedIndex = 1;
        _editor.Attach(original, 0); _editor.MarkSaved(); Render(); await VisualCapture.Paint();
    }
}
