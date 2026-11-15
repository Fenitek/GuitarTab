using System.Windows;
using GuitarStudio.Diagnostics;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private static void RequireLayout(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Autotest layout: " + message);
    }

    private async Task VerifyOverlayScrollGuard(LiveLog log)
    {
        var original = _editor.Score!;
        var track = _editor.TrackIndex;
        var bar = _editor.BarIndex;
        var beat = (int)(_editor.CurrentBeat?.Index ?? 0);
        var line = _editor.Line;
        var dirty = _editor.IsDirty;
        var height = Tab.Height;
        var width = Tab.Width;
        try
        {
            _editor.Attach(PerformanceScore.Create(Api.Settings, 64, 1), 0);
            Tab.Width = 520;
            Render();
            Tab.Height = 70;
            UpdateLayout();
            await VisualCapture.Paint();
            var sv = ScrollView!;
            RequireLayout(sv.ViewportHeight < 90, "la prova deve forzare una partitura troppo bassa");
            var start = _overlayAutoScrollCount;
            _editor.MoveTo(32, 0, _editor.StringCount);
            for (var i = 0; i < 64; i++) UpdateOverlay();
            await VisualCapture.Paint();
            RequireLayout(_overlayAutoScrollCount == start, "nessuno scorrimento automatico nel viewport troppo basso");
            log.AppendLine($"overlay OK: viewport forzato a {sv.ViewportHeight:0.0} px, 64 aggiornamenti, 0 richieste di scroll");

            Tab.Height = 240;
            _editor.MoveTo(0, 0, 1);
            UpdateLayout();
            await VisualCapture.Paint();
            sv.ScrollToTop();
            await VisualCapture.Paint();
            start = _overlayAutoScrollCount;
            _editor.MoveTo(32, 0, 1);
            for (var i = 0; i < 64; i++) UpdateOverlay();
            RequireLayout(_overlayAutoScrollCount - start == 1, "una sola correzione durante ScrollChanged e aggiornamenti rientranti");
            await VisualCapture.Paint();
            var stable = _overlayAutoScrollCount;
            var offset = sv.VerticalOffset;
            for (var i = 0; i < 64; i++) UpdateOverlay();
            await VisualCapture.Paint();
            RequireLayout(_overlayAutoScrollCount == stable && Math.Abs(sv.VerticalOffset - offset) < 1,
                "lo scorrimento deve fermarsi quando il cursore è visibile");
            RequireLayout(!_overlayScrollPending, "la protezione deve essere rilasciata dal dispatcher");
            log.AppendLine("overlay OK: 1 correzione per il cursore fuori vista, nessun ciclo dopo altri 64 aggiornamenti");
            sv.ScrollToTop();
            await VisualCapture.Paint();
            RequireLayout(_overlayAutoScrollCount == stable && sv.VerticalOffset < 1,
                "ScrollChanged ridisegna senza annullare lo scorrimento manuale");
            log.AppendLine("overlay OK: ScrollChanged non innesca altri scroll e conserva lo scorrimento manuale");
        }
        finally
        {
            Tab.Height = height;
            Tab.Width = width;
            _editor.Attach(original, track, isDirty: dirty);
            _editor.MoveTo(bar, beat, line);
            Render();
            await VisualCapture.Paint();
        }
    }
}
