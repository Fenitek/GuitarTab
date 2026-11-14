using System.Diagnostics;
using System.Windows.Threading;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private DispatcherTimer? _editorRenderTimer;
    private double _lastDeferredRenderMs;
    private int _editorRenderCount;

    // Il renderer WPF non supporta il layout incrementale/lazy. Lascia prima passare
    // l'input e accorpa gli edit ravvicinati in un solo render del modello più recente.
    // Tutto rimane sul dispatcher: nessun worker e nessuna copia vecchia contro lo Score nuovo.
    private void ScheduleEditorRender()
    {
        if (_editorRenderTimer == null)
        {
            _editorRenderTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _editorRenderTimer.Tick += (_, _) =>
            {
                _editorRenderTimer.Stop();
                var sw = Stopwatch.StartNew();
                Render();
                _lastDeferredRenderMs = sw.Elapsed.TotalMilliseconds;
                _editorRenderCount++;
            };
        }
        _editorRenderTimer.Stop();
        _editorRenderTimer.Start();
    }

    private void CancelPendingEditorRender() => _editorRenderTimer?.Stop();
}
