using AlphaTab;
using AlphaTab.Model;
using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class HistoryTests
{
    [Fact]
    public void LocalUndoKeepsScoreAndUntouchedBarsAndTrackAlive()
    {
        var e = Create();
        e.AddTrack("Seconda", 25, new double[] { 64, 59, 55, 50, 45, 40 }, false);
        e.SelectTrack(0);
        var score = e.Score;
        var untouched = e.Staff!.Bars[3];
        var otherTrack = e.Score!.Tracks[1];
        Change(e, () => e.SetFret(12), () =>
        {
            Assert.Same(score, e.Score);
            Assert.Same(untouched, e.Staff!.Bars[3]);
            Assert.Same(otherTrack, e.Score!.Tracks[1]);
        });
    }

    [Theory]
    [InlineData(NoteEffect.HammerPull)] [InlineData(NoteEffect.Slide)] [InlineData(NoteEffect.SlideShift)]
    public void UndoDeletingDestinationRestoresLinkAcrossBars(NoteEffect effect)
    {
        var e = Create(". :4 3.1 r r r | 5.1 r r r");
        Assert.True(e.ToggleEffect(effect));
        e.MoveTo(1, 0, 1);
        Change(e, e.DeleteAtCursor, () => Assert.Empty(e.CurrentBeat!.Notes));
        e.Undo();
        var origin = e.Staff!.Bars[0].Voices[0].Beats[0].Notes[0];
        Assert.True(TabEditor.HasEffect(origin.Beat, origin, effect));
        Assert.Equal(5, e.CurrentNote!.Fret);
        AssertModel(e.Score!);
    }

    [Fact]
    public void LocalHistoryPreservesTextLyricsAutomationAndOtherVoice()
    {
        var e = Create();
        e.SelectVoice(1);
        e.SetFret(19);
        e.SelectVoice(0);
        var beat = e.CurrentBeat!;
        beat.Text = "Nota importata";
        beat.Lyrics = new List<string> { "la", "voce" };
        beat.Automations.Add(new Automation { Type = AutomationType.Volume, Value = 11, RatioPosition = 0.5 });
        for (var i = 0; i < 3; i++)
        {
            e.SetFret(12);
            e.Undo();
            Assert.Equal("Nota importata", e.CurrentBeat!.Text);
            Assert.Equal(new[] { "la", "voce" }, e.CurrentBeat.Lyrics);
            var automation = Assert.Single(e.CurrentBeat.Automations, a => a.Type == AutomationType.Volume);
            Assert.Equal(11, automation.Value);
            Assert.Equal(0.5, automation.RatioPosition);
            Assert.Equal(19, e.CurrentBar!.Voices[1].Beats[0].Notes[0].Fret);
            e.Redo();
            AssertModel(e.Score!);
        }
    }

    [Fact]
    public void MixedLocalAndStructuralSnapshotsUndoAndRedoInOrder()
    {
        var e = Create();
        var states = new List<string> { Content(e.Score!) };
        Action[] actions =
        [
            () => e.SetFret(12), e.InsertBarAfter, () => e.SetFret(15),
            () => e.AddTrack("Basso", 33, new double[] { 43, 38, 33, 28 }, true),
            () => e.SetFret(7), () => e.SetBarInfo(e.GetBarInfo() with { Numerator = 3 }),
            e.DeleteTrack, () => e.SetFret(24)
        ];
        foreach (var action in actions) { action(); AssertModel(e.Score!); states.Add(Content(e.Score!)); }
        for (var i = actions.Length - 1; i >= 0; i--)
        {
            e.Undo(); AssertModel(e.Score!); Assert.Equal(states[i], Content(e.Score!));
        }
        for (var i = 1; i <= actions.Length; i++)
        {
            e.Redo(); AssertModel(e.Score!); Assert.Equal(states[i], Content(e.Score!));
        }
    }

    [Fact]
    public void ImportBetweenLocalEditsCannotCreateDuplicateIds()
    {
        var e = Create();
        for (var i = 0; i < 5; i++)
        {
            e.SetFret(12);
            e.Undo();
            _ = Read(e.ExportGp()); // ScoreLoader resets alphaTab's process-wide ID counters.
            e.Copy(); e.Paste();
            var score = Read(e.ExportGp());
            Assert.Equal(Content(e.Score!), Content(score));
            AssertModel(e.Score!);
        }
    }
}
