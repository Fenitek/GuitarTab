using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class ChordTextTests
{
    private const string Open = ". :4 (0.1 1.2 0.3 2.4 3.5) r r r | 3.1 r r r";

    [Fact]
    public void ChordIsSuggestedFromTheNotesAndSurvivesUndoAndGp()
    {
        var e = Create(Open);
        var score = e.Score;
        var suggestion = e.SuggestChord();
        Assert.Equal("x 3 2 0 1 0", TabEditor.FormatFrets(suggestion.FretsLowToHigh));
        Assert.True(suggestion.ShowDiagram);

        Change(e, () => Assert.True(e.SetChord(suggestion with { Name = "Do" })), () =>
        {
            var chord = e.CurrentBeat!.Chord!;
            Assert.Equal("Do", chord.Name);
            Assert.True(chord.ShowDiagram);
            Assert.Equal(new double[] { 0, 1, 0, 2, 3, -1 }, chord.Strings);
            Assert.Equal("x 3 2 0 1 0", TabEditor.FormatFrets(e.CurrentChord!.FretsLowToHigh));
            Assert.Same(score, e.Score);
        });
        Assert.False(e.SetChord(e.CurrentChord!));
    }

    [Fact]
    public void ChordNameOnlyHighFretsAndRemoval()
    {
        var e = Create(Open);
        e.MoveTo(1, 0, 1);
        var frets = TabEditor.ParseFrets("x x 7 9 10 9", 6)!;
        Change(e, () => e.SetChord(new ChordInfo("La7", false, frets)), () =>
        {
            Assert.False(e.CurrentBeat!.Chord!.ShowDiagram);
            Assert.Equal(7, e.CurrentBeat.Chord.FirstFret);
        });
        Change(e, () => e.SetChord(new ChordInfo("La7", true, frets)), () => Assert.True(e.CurrentBeat!.Chord!.ShowDiagram));
        Assert.Single(e.Staff!.Chords!.Keys());
        Change(e, () => Assert.True(e.SetChord(null)), () =>
        {
            Assert.False(e.CurrentBeat!.HasChord);
            Assert.True(e.Staff!.Chords?.Keys().Any() != true);
        });
        Assert.False(e.SetChord(null));
    }

    [Fact]
    public void IdenticalChordsShareTheDefinitionAndPasteCarriesItToAnotherTrack()
    {
        var e = Create(Open);
        var chord = new ChordInfo("Do", true, TabEditor.ParseFrets("x 3 2 0 1 0", 6)!);
        e.SetChord(chord);
        e.MoveTo(0, 2, 1);
        e.SetChord(chord);
        Assert.Single(e.Staff!.Chords!.Keys());
        e.MoveTo(0, 0, 1);
        e.Copy();
        e.AddTrack("Seconda", 25, new double[] { 64, 59, 55, 50, 45, 40 }, false);
        Change(e, e.Paste, () =>
        {
            Assert.Equal("Do", e.Staff!.Bars[0].Voices[0].Beats[0].Chord!.Name);
            Assert.Single(e.Staff.Chords!.Keys());
        });
        e.Undo();
        Assert.True(e.Staff!.Chords == null || !e.Staff.Chords.Keys().Any());
    }

    [Theory]
    [InlineData("x 3 2 0 1", false)] [InlineData("x 3 2 0 1 0", true)] [InlineData("x,3,2,0,1,0", true)]
    [InlineData("x 3 2 0 1 25", false)] [InlineData("a 3 2 0 1 0", false)]
    public void FretsParsing(string text, bool valid) => Assert.Equal(valid, TabEditor.ParseFrets(text, 6) != null);

    [Fact]
    public void BeatTextSetChangeRemoveWithUndoAndGp()
    {
        var e = Create();
        var score = e.Score;
        e.MoveTo(1, 2, 1);
        Change(e, () => Assert.True(e.SetBeatText("  rallentando  ")), () =>
        {
            Assert.Equal("rallentando", e.CurrentBeat!.Text);
            Assert.Same(score, e.Score);
        });
        Assert.False(e.SetBeatText("rallentando"));
        Change(e, () => e.SetBeatText("a tempo"), () => Assert.Equal("a tempo", e.CurrentBeat!.Text));
        Change(e, () => Assert.True(e.SetBeatText("")), () => Assert.True(string.IsNullOrEmpty(e.CurrentBeat!.Text)));
        Assert.False(e.SetBeatText(null));
    }
}
