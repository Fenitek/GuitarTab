using AlphaTab.Midi;
using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class AlternateEndingTests
{
    // Una nota diversa per battuta (corda 1: 64 + tasto), così l'ordine MIDI dice l'ordine delle battute.
    private const string Song = ". \\ro :1 1.1 | :1 2.1 | :1 3.1 | :1 4.1 | :1 5.1 | :1 6.1";

    private static int[] PlayedBars(byte[] gp) => DynamicsGraceTests.Midi(Read(gp)).Events.OfType<NoteOnEvent>()
        .OrderBy(n => n.Tick).Select(n => (int)n.NoteKey - 64).ToArray();

    [Fact]
    public void FirstAndSecondEndingFollowTheRepeatInPlayback()
    {
        var e = Create(Song);
        var score = e.Score;
        e.MoveTo(2, 0, 1);
        Assert.Null(e.AlternateEndingProblem(1));
        Change(e, () =>
        {
            Assert.True(e.ToggleAlternateEnding(1, out var added));
            Assert.True(added);
        }, () =>
        {
            Assert.Equal([1], TabEditor.AlternateEndingsOf(e.Score!.MasterBars[2]));
            Assert.Equal(2, e.Score.MasterBars[2].RepeatCount);
            Assert.Same(score, e.Score);
        });

        e.MoveTo(3, 0, 1);
        e.MoveTo(4, 0, 1, extendSelection: true);
        Assert.Null(e.AlternateEndingProblem(2));
        Change(e, () => Assert.True(e.ToggleAlternateEnding(2, out _)), () =>
        {
            Assert.Equal([2], TabEditor.AlternateEndingsOf(e.Score!.MasterBars[3]));
            Assert.Equal([2], TabEditor.AlternateEndingsOf(e.Score.MasterBars[4]));
            Assert.Equal("2ª volta", TabEditor.AlternateEndingsLabel(e.Score.MasterBars[4]));
        });

        // Ritornello dalla battuta 1: 1 2 3(1ª) | 1 2 4 5(2ª) 6.
        Assert.Equal([1, 2, 3, 1, 2, 4, 5, 6], PlayedBars(e.ExportGp()));
        e.Undo();
        Assert.Equal([1, 2, 3, 1, 2, 4, 5, 6], PlayedBars(e.ExportGp())); // con la sola 1ª volta la battuta 3 si salta comunque
        e.Undo();
        Assert.Equal([1, 2, 3, 4, 5, 6], PlayedBars(e.ExportGp()));
    }

    [Fact]
    public void EndingsRespectAnExplicitRepeatStartAndThreePasses()
    {
        var e = Create(Song);
        e.MoveTo(1, 0, 1);
        e.SetBarInfo(e.GetBarInfo() with { RepeatStart = true });
        e.MoveTo(3, 0, 1);
        e.SetBarInfo(e.GetBarInfo() with { RepeatCount = 3 });
        e.MoveTo(3, 0, 1);
        Assert.True(e.ToggleAlternateEnding(1, out var added));
        Assert.False(added);
        Assert.True(e.ToggleAlternateEnding(2, out _));
        e.MoveTo(4, 0, 1);
        Assert.True(e.ToggleAlternateEnding(3, out _));
        Assert.Equal([1, 2], TabEditor.AlternateEndingsOf(e.Score!.MasterBars[3]));
        Assert.Equal("1ª volta, 2ª volta", TabEditor.AlternateEndingsLabel(e.Score.MasterBars[3]));
        // 1 | 2 3 4 | 2 3 4 | 2 3 5 6
        Assert.Equal([1, 2, 3, 4, 2, 3, 4, 2, 3, 5, 6], PlayedBars(e.ExportGp()));
    }

    [Fact]
    public void IncoherentEndingsAreRefusedWithAnExplanation()
    {
        var e = Create(Song);
        e.MoveTo(0, 0, 1);
        Assert.NotNull(e.AlternateEndingProblem(1));          // nessuna parte comune prima
        Assert.False(e.ToggleAlternateEnding(1, out _));
        Assert.False(e.CanUndo);
        e.MoveTo(3, 0, 1);
        Assert.NotNull(e.AlternateEndingProblem(2));          // manca la 1ª volta prima
        e.MoveTo(2, 0, 1);
        e.SetBarInfo(e.GetBarInfo() with { RepeatCount = 2 });
        e.MoveTo(1, 0, 1);
        e.MoveTo(3, 0, 1, extendSelection: true);
        Assert.NotNull(e.AlternateEndingProblem(1));          // il :‖ è a metà selezione
        e.MoveTo(2, 0, 1);
        e.ToggleAlternateEnding(1, out _);
        e.MoveTo(3, 0, 1);
        Assert.NotNull(e.AlternateEndingProblem(3));          // nessuna 3ª volta con due ripetizioni
        Assert.Null(e.AlternateEndingProblem(2));
    }

    [Fact]
    public void ToggleRemovesTheEndingWithLocalUndo()
    {
        var e = Create(Song);
        e.MoveTo(2, 0, 1);
        e.ToggleAlternateEnding(1, out _);
        Change(e, () => Assert.True(e.ToggleAlternateEnding(1, out _)), () =>
        {
            Assert.Empty(TabEditor.AlternateEndingsOf(e.Score!.MasterBars[2]));
            Assert.Equal(2, e.Score.MasterBars[2].RepeatCount);
        });
    }
}
