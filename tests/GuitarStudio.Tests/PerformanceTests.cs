using System.Diagnostics;
using System.Text.Json;
using AlphaTab;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;
using Xunit.Abstractions;

namespace GuitarStudio.Tests;

public sealed class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void StructuralOperationsOn200BarsAnd4Tracks()
    {
        var settings = new Settings();
        var e = new TabEditor(settings);
        e.Attach(PerformanceScore.Create(settings), 0);
        var samples = new List<object>();
        void Measure(string operation, Action prepare, Action action)
        {
            for (var sample = -1; sample < 10; sample++)
            {
                prepare();
                var sw = Stopwatch.StartNew(); action(); var edit = sw.Elapsed.TotalMilliseconds;
                EditorFixture.AssertModel(e.Score!);
                sw.Restart(); e.Undo(); var undo = sw.Elapsed.TotalMilliseconds;
                EditorFixture.AssertModel(e.Score!);
                sw.Restart(); e.Redo(); var redo = sw.Elapsed.TotalMilliseconds;
                EditorFixture.AssertModel(e.Score!);
                e.Undo();
                if (sample >= 0) samples.Add(new { Operation = operation, EditMs = edit, UndoMs = undo, RedoMs = redo });
            }
        }
        Measure("InsertBarAfter", () => e.MoveTo(99, 0, 1), e.InsertBarAfter);
        Measure("DeleteBar", () => e.MoveTo(99, 0, 1), e.DeleteBar);
        e.MoveTo(99, 0, 1); e.Copy();
        Measure("PasteWholeBars", () => e.MoveTo(99, 0, 1), e.Paste);
        Measure("MoveRightEnd", () => e.MoveTo(199, 7, 1), () => e.MoveRight());
        var json = JsonSerializer.Serialize(new { Bars = 200, Tracks = 4, Samples = samples }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (System.Environment.GetEnvironmentVariable("GS_PERF_STRUCTURAL_REPORT") is { } path) File.WriteAllText(path, json);
    }

    [Fact]
    public void Generated200BarsAnd4TracksMeasureEditingAndUndo()
    {
        var settings = new Settings();
        var e = new TabEditor(settings);
        e.Attach(PerformanceScore.Create(settings), 0);
        Assert.Equal(200, e.Score!.MasterBars.Count);
        Assert.Equal(4, e.Score.Tracks.Count);
        EditorFixture.AssertModel(e.Score);
        e.MoveTo(99, 0, 1);
        // JIT e inizializzazioni alphaTab esclusi dai campioni.
        e.SetFret(12); e.Undo(); e.InsertBarAfter(); e.Undo();
        var samples = new List<object>();
        for (var i = 0; i < 10; i++)
        {
            e.MoveTo(99, 0, 1);
            var sw = Stopwatch.StartNew();
            e.SetFret(12 + i % 2);
            var set = sw.Elapsed.TotalMilliseconds;
            Assert.Equal(12 + i % 2, e.CurrentNote!.Fret);
            EditorFixture.AssertModel(e.Score!);
            sw.Restart();
            e.Undo();
            var undo = sw.Elapsed.TotalMilliseconds;
            Assert.Equal(3, e.CurrentNote!.Fret);
            EditorFixture.AssertModel(e.Score!);
            e.MoveTo(99, 0, 1);
            sw.Restart();
            e.InsertBarAfter();
            var insert = sw.Elapsed.TotalMilliseconds;
            Assert.Equal(201, e.Score!.MasterBars.Count);
            EditorFixture.AssertModel(e.Score);
            e.Undo();
            Assert.Equal(200, e.Score!.MasterBars.Count);
            EditorFixture.AssertModel(e.Score);
            samples.Add(new { SetFretMs = set, InsertBarAfterMs = insert, UndoMs = undo });
        }
        var json = JsonSerializer.Serialize(new { Bars = 200, Tracks = 4, Samples = samples }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        if (System.Environment.GetEnvironmentVariable("GS_PERF_MODEL_REPORT") is { } path) File.WriteAllText(path, json);
    }
}
