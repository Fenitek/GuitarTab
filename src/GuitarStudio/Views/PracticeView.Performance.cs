using System.Diagnostics;
using System.IO;
using System.Text.Json;
using GuitarStudio.Diagnostics;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private sealed record EditorChangeTiming(double TotalMs, double RenderMs, double LoadMidiForScoreMs, double RebuildTracksPanelMs);
    private EditorChangeTiming? _lastChangeTiming;
    private bool _performanceStarted;
    private sealed record PerformanceSample(string Operation, double KeyMs, EditorChangeTiming? Change,
        double DeferredRenderMs, double DeferredMidiMs);

    // GS_BENCHMARK=1 esegue una misura senza interazioni sul desktop e chiude la sua istanza.
    private async void MaybeRunPerformanceBenchmark()
    {
        if (_performanceStarted || Environment.GetEnvironmentVariable("GS_BENCHMARK") != "1") return;
        _performanceStarted = true;
        var initialErrors = App.UnhandledErrorCount;
        var path = Environment.GetEnvironmentVariable("GS_PERF_APP_REPORT") ?? Path.Combine(Path.GetTempPath(), "gs_performance.json");
        try
        {
            await Task.Delay(1000);
            Api.Stop();
            _editor.Attach(PerformanceScore.Create(Api.Settings), 0);
            Render();
            RebuildTracksPanel();
            SetEditMode(true);
            await Task.Delay(1500);
            _editor.MoveTo(99, 0, 1);
            TypeDigit(4); _editor.Undo(); _editor.InsertBarAfter(); _editor.Undo();
            await Task.Delay(500);
            var samples = new List<object>();
            async Task Measure(string operation, Action action)
            {
                var sw = Stopwatch.StartNew();
                action();
                var keyMs = sw.Elapsed.TotalMilliseconds;
                var timing = _lastChangeTiming;
                await Task.Delay(450);
                samples.Add(new PerformanceSample(operation, keyMs, timing, _lastDeferredRenderMs, _lastDeferredMidiMs));
            }
            for (var i = 0; i < 10; i++)
            {
                _editor.MoveTo(99, 0, 1);
                _lastDigit = null;
                await Measure("SetFret", () => TypeDigit(5 + i % 2));
                await Measure("Undo", _editor.Undo);
                _editor.MoveTo(99, 0, 1);
                await Measure("InsertBarAfter", _editor.InsertBarAfter);
                _editor.Undo();
                await Task.Delay(400);
            }
            var generations = _midiGenerationCount;
            var renders = _editorRenderCount;
            var builds = _tracksPanelBuildCount;
            for (var i = 0; i < 12; i++)
            {
                _editor.SetFret(10 + i % 10);
                if (_midiReady) throw new InvalidOperationException("Anteprime abilitate durante una raffica di modifiche.");
            }
            await Task.Delay(500);
            if (_midiGenerationCount != generations + 1 || !_midiReady)
                throw new InvalidOperationException("La raffica deve generare un solo MIDI aggiornato e pronto.");
            if (_tracksPanelBuildCount != builds || _editorRenderCount != renders + 1)
                throw new InvalidOperationException("La raffica deve riusare il pannello e accorpare il ridisegno.");
            if (App.UnhandledErrorCount != initialErrors) throw new InvalidOperationException("Eccezioni WPF durante il benchmark.");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Bars = 200, Tracks = 4, UseWorkers = Api.Settings.Core.UseWorkers,
                Version = AppInfo.Version, Samples = samples,
                Burst = new { Keys = 12, MidiLoads = _midiGenerationCount - generations,
                    Renders = _editorRenderCount - renders, PanelBuilds = _tracksPanelBuildCount - builds, MidiReady = _midiReady }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            App.Log(ex);
            File.WriteAllText(path, JsonSerializer.Serialize(new { Error = ex.ToString() }));
        }
        finally
        {
            Focus();
            _editor.MarkSaved();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
