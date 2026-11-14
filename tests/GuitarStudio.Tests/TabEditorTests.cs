using AlphaTab.Model;
using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public class TabEditorTests
{
    [Theory]
    [InlineData(0)] [InlineData(9)] [InlineData(10)] [InlineData(12)] [InlineData(24)] [InlineData(30)]
    public void InsertAndReplaceFrets(int fret)
    {
        var e = Create();
        e.MoveTo(0, 0, 2);
        Change(e, () => e.SetFret(fret), () => Assert.Equal(fret, e.CurrentNote!.Fret));
        Assert.Equal(2, e.CurrentBeat!.Notes.Count);
        Change(e, () => e.SetFret(18), () => Assert.Equal(18, e.CurrentNote!.Fret));
        Assert.Equal(2, e.CurrentBeat!.Notes.Count);
    }

    [Theory]
    [InlineData(Duration.Whole)] [InlineData(Duration.Half)] [InlineData(Duration.Eighth)]
    [InlineData(Duration.Sixteenth)] [InlineData(Duration.ThirtySecond)] [InlineData(Duration.SixtyFourth)]
    public void DurationsDotAndTriplet(Duration duration)
    {
        var e = Create();
        Change(e, () => e.SetDuration(duration), () => Assert.Equal(duration, e.CurrentBeat!.Duration));
        Change(e, e.ToggleDot, () => Assert.Equal(1, e.CurrentBeat!.Dots));
        Change(e, e.ToggleDot, () => Assert.Equal(0, e.CurrentBeat!.Dots));
        Change(e, () => e.ToggleTuplet(), () =>
        {
            Assert.Equal(3, e.CurrentBeat!.TupletNumerator);
            Assert.Equal(2, e.CurrentBeat.TupletDenominator);
        });
        Change(e, () => e.ToggleTuplet(), () => Assert.False(e.CurrentBeat!.HasTuplet));
    }

    [Theory]
    [InlineData(". :4 3.1")]
    [InlineData(". :4 3.1 4.1 5.1 6.1 7.1")]
    [InlineData(". :2 3.1 :1 5.1")]
    [InlineData(". \\ts 3 4 :1 3.1")]
    public void FixBarFillsAndTrims(string tex)
    {
        var e = Create(tex);
        Change(e, e.FixBar, () =>
        {
            Assert.True(e.BarFill(out var used, out var total));
            Assert.Equal(total, used);
            Assert.Equal(3, e.CurrentVoice!.Beats[0].Notes[0].Fret);
        });
    }

    [Fact]
    public void InsertAndDeleteBeatsAndBars()
    {
        var e = Create();
        e.MoveTo(0, 2, 1);
        Change(e, e.InsertBeat, () => Assert.Equal(5, e.CurrentVoice!.Beats.Count));
        e.MoveTo(0, 2, 1);
        Change(e, e.DeleteBeat, () => Assert.Equal(4, e.CurrentVoice!.Beats.Count));
        Change(e, e.InsertBarAfter, () => Assert.Equal(5, e.Score!.MasterBars.Count));
        e.MoveTo(1, 0, 1);
        Change(e, e.DeleteBar, () => Assert.Equal(4, e.Score!.MasterBars.Count));
    }

    [Fact]
    public void InsertBarAndTrackFillOddMeterExactly()
    {
        var e = Create(". \\ts 3 8 :8 3.1 4.1 5.1");
        Change(e, e.InsertBarAfter, () => Assert.Equal(1440, e.Staff!.Bars[1].Voices[0].CalculateDuration()));
        Change(e, () => e.AddTrack("Basso", 33, new double[] { 43, 38, 33, 28 }, true), () =>
            Assert.All(e.Score!.Tracks[1].Staves[0].Bars, b => Assert.Equal(1440, b.Voices[0].CalculateDuration())));
    }

    [Fact]
    public void WholeBarCopyCutAndPaste()
    {
        var e = Create();
        Assert.Equal(4, e.Copy());
        Change(e, e.Paste, () =>
        {
            Assert.Equal(5, e.Score!.MasterBars.Count);
            Assert.Equal(new double[] { 3, 5, 7, 8 }, Frets(e, 1));
        });
        e.MoveTo(0, 0, 1);
        e.MoveTo(1, 3, 1, true);
        Assert.True(e.SelectionIsWholeBars(out _, out _));
        Change(e, e.Cut, () =>
        {
            Assert.Equal(5, e.Score!.MasterBars.Count);
            Assert.All(e.Staff!.Bars.Take(2), b => Assert.All(b.Voices[0].Beats, beat => Assert.Empty(beat.Notes)));
        });
        e.MoveTo(0, 0, 1);
        Change(e, e.Paste, () =>
        {
            Assert.Equal(new double[] { 3, 5, 7, 8 }, Frets(e, 0));
            Assert.Equal(new double[] { 3, 5, 7, 8 }, Frets(e, 1));
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PartialSelectionAcrossBars(bool cut)
    {
        var e = Create();
        e.MoveTo(0, 2, 1);
        e.MoveTo(1, 1, 1, true);
        Assert.False(e.SelectionIsWholeBars(out _, out _));
        if (cut)
            Change(e, e.Cut, () =>
            {
                Assert.Equal(new double[] { 3, 5 }, Frets(e, 0));
                Assert.Equal(new double[] { 14, 15 }, Frets(e, 1));
            });
        else Assert.Equal(4, e.Copy());
        e.MoveTo(2, 1, 1);
        Change(e, e.Paste, () => Assert.Equal(new double[] { 1, 7, 8, 10, 12, 2, 3, 4 }, Frets(e, 2)));
    }

    [Fact]
    public void SecondVoiceCutAndPastePreservesFirstVoice()
    {
        var e = Create();
        var first = Frets(e, 0);
        Change(e, () => e.SelectVoice(1), () => Assert.Equal(2, e.CurrentBar!.Voices.Count));
        e.SelectVoice(1);
        Change(e, () => e.SetFret(12), () => Assert.Equal(12, e.CurrentNote!.Fret));
        Change(e, e.Cut, () =>
        {
            Assert.Equal(first, Frets(e, 0));
            Assert.All(e.Staff!.Bars[0].Voices[1].Beats, b => Assert.Empty(b.Notes));
        });
        e.SelectVoice(1);
        e.MoveTo(0, 0, 1);
        Change(e, e.Paste, () =>
        {
            Assert.Equal(first, Frets(e, 0));
            Assert.Equal(12, e.Staff!.Bars[0].Voices[1].Beats[0].Notes[0].Fret);
        });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void PartialCutOnlyChangesActiveVoice(int activeVoice)
    {
        var e = Create();
        e.SelectVoice(1);
        e.MoveTo(0, 0, 1);
        Change(e, () => e.SetFret(20), () => Assert.Equal(20, e.CurrentNote!.Fret));
        e.MoveTo(0, 1, 1);
        Change(e, () => e.SetFret(22), () => Assert.Equal(22, e.CurrentNote!.Fret));
        var other = e.Staff!.Bars[0].Voices[1 - activeVoice].Beats.SelectMany(b => b.Notes).Select(n => n.Fret).ToArray();
        e.SelectVoice(activeVoice);
        e.MoveTo(0, 0, 1);
        e.MoveTo(0, 1, 1, true);
        Change(e, e.Cut, () =>
        {
            Assert.Equal(2, e.Staff!.Bars[0].Voices[activeVoice].Beats.Count);
            Assert.Equal(other, e.Staff.Bars[0].Voices[1 - activeVoice].Beats.SelectMany(b => b.Notes).Select(n => n.Fret));
        });
    }

    [Fact]
    public void MoveRightAppendsRestThenBarAndCanUndoBoth()
    {
        var e = Create(". :4 3.1");
        Change(e, () => e.MoveRight(), () => Assert.Equal(2, e.CurrentVoice!.Beats.Count));
        e.MoveTo(0, 1, 1);
        Change(e, e.FixBar, () => Assert.Equal(3840, e.CurrentVoice!.CalculateDuration()));
        e.LastBeat();
        Change(e, () => e.MoveRight(), () => Assert.Equal(2, e.Score!.MasterBars.Count));
    }

    [Theory]
    [InlineData(NoteEffect.HammerPull)] [InlineData(NoteEffect.Slide)] [InlineData(NoteEffect.SlideShift)]
    public void LinkWarningRequiresDestinationInSameVoiceAndString(NoteEffect effect)
    {
        var e = Create(". :4 3.1 5.2 | 7.1 r r r");
        Assert.Null(e.LinkWarning(effect)); // Same string in the next bar.
        Change(e, () => e.ToggleEffect(effect), () => Assert.True(TabEditor.HasEffect(e.CurrentBeat, e.CurrentNote, effect)));
        Assert.Null(e.LinkWarning(effect)); // Disabling an existing link is allowed.
        e.MoveTo(1, 0, 1);
        Assert.NotNull(e.LinkWarning(effect));
        e.MoveTo(0, 0, 1);
        e.SelectVoice(1);
        Change(e, () => e.SetFret(12), () => Assert.Equal(12, e.CurrentNote!.Fret));
        Assert.NotNull(e.LinkWarning(effect)); // Voice 1 has destination notes, voice 2 does not.
    }

    [Fact]
    public void TieRequiresPreviousNoteOnSameString()
    {
        var e = Create();
        Assert.NotNull(e.LinkWarning(NoteEffect.Tie));
        e.MoveTo(0, 1, 1);
        Assert.Null(e.LinkWarning(NoteEffect.Tie));
        Change(e, () => e.ToggleEffect(NoteEffect.Tie), () => Assert.True(e.CurrentNote!.IsTieDestination));
    }

    [Fact]
    public void TimeSignaturePropagatesUntilExistingChange()
    {
        var e = Create(". \\ts 4 4 :4 3.1 4.1 5.1 6.1 | 3.1 4.1 5.1 6.1 | \\ts 3 4 3.1 4.1 5.1");
        Change(e, () => e.SetBarInfo(e.GetBarInfo() with { Numerator = 6, Denominator = 8, TempoChange = 85 }), () =>
        {
            Assert.Equal(new double[] { 6, 6, 3 }, e.Score!.MasterBars.Select(m => m.TimeSignatureNumerator));
            Assert.Equal(new double[] { 8, 8, 4 }, e.Score.MasterBars.Select(m => m.TimeSignatureDenominator));
            Assert.Equal(85, e.TempoAt(1));
        });
    }

    [Fact]
    public void AddAndDeleteTrackAndProtectLastTrack()
    {
        var e = Create();
        Change(e, () => e.AddTrack("Basso", 33, new double[] { 43, 38, 33, 28 }, true), () =>
        {
            Assert.Equal(2, e.Score!.Tracks.Count);
            Assert.Equal(4, e.Score.Tracks[1].Staves[0].Tuning.Count);
            Assert.DoesNotContain(9, e.Score.Tracks.SelectMany(t => new[] { t.PlaybackInfo.PrimaryChannel, t.PlaybackInfo.SecondaryChannel }));
        });
        e.SelectTrack(1);
        Change(e, e.DeleteTrack, () => Assert.Single(e.Score!.Tracks));
        e.DeleteTrack();
        Assert.Single(e.Score!.Tracks);
    }

    [Fact]
    public void NewEditDiscardsRedoAndMarkSavedResetsDirtyFlag()
    {
        var e = Create();
        e.SetFret(12);
        e.Undo();
        Assert.True(e.CanRedo);
        e.SetFret(24);
        Assert.False(e.CanRedo);
        e.MarkSaved();
        Assert.False(e.IsDirty);
    }

    private static double[] Frets(TabEditor e, int bar) => e.Staff!.Bars[bar].Voices[0].Beats.SelectMany(b => b.Notes).Select(n => n.Fret).ToArray();
}
