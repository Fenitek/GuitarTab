using AlphaTab;
using AlphaTab.Midi;
using AlphaTab.Model;
using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class DynamicsGraceTests
{
    internal static MidiFile Midi(Score score)
    {
        var midi = new MidiFile();
        new MidiFileGenerator(score, new Settings(), new AlphaSynthMidiFileHandler(midi, true)).Generate();
        return midi;
    }

    private static List<Beat> Chain(TabEditor e) => e.Staff!.Bars.SelectMany(b => b.Voices[0].Beats).ToList();

    [Fact]
    public void DynamicsLastUntilTheNextChangeAndSurviveUndoAndGp()
    {
        var e = Create();
        var score = e.Score;
        e.MoveTo(1, 2, 1);
        Change(e, () => Assert.True(e.SetDynamics(DynamicValue.PP)), () =>
        {
            var chain = Chain(e);
            Assert.All(chain.Take(6), b => Assert.Equal(DynamicValue.F, b.Dynamics));
            Assert.All(chain.Skip(6), b => Assert.Equal(DynamicValue.PP, b.Dynamics));
            Assert.Same(score, e.Score);
        });

        // Un cambio successivo sulla stessa linea si ferma al segno già presente.
        e.MoveTo(0, 1, 1);
        Change(e, () => Assert.True(e.SetDynamics(DynamicValue.MP)), () =>
        {
            var chain = Chain(e);
            Assert.Equal(DynamicValue.F, chain[0].Dynamics);
            Assert.All(chain.Skip(1).Take(5), b => Assert.Equal(DynamicValue.MP, b.Dynamics));
            Assert.All(chain.Skip(6), b => Assert.Equal(DynamicValue.PP, b.Dynamics));
        });
        Assert.False(e.SetDynamics(DynamicValue.MP));
    }

    [Fact]
    public void SelectionGetsTheDynamicAndStepsAreClamped()
    {
        var e = Create();
        e.MoveTo(2, 0, 1);
        e.MoveTo(2, 3, 1, extendSelection: true);
        Change(e, () => Assert.True(e.SetDynamics(DynamicValue.FFF)), () =>
            Assert.All(e.Staff!.Bars.Skip(2).SelectMany(b => b.Voices[0].Beats), b => Assert.Equal(DynamicValue.FFF, b.Dynamics)));
        e.MoveTo(3, 0, 1);
        Assert.False(e.StepDynamics(+1));
        Change(e, () => Assert.True(e.StepDynamics(-1)), () => Assert.Equal(DynamicValue.FF, e.CurrentBeat!.Dynamics));
        e.MoveTo(0, 0, 1);
        for (var i = 0; i < 10; i++) e.StepDynamics(-1);
        Assert.Equal(DynamicValue.PPP, e.CurrentBeat!.Dynamics);
        Assert.False(e.StepDynamics(-1));
    }

    [Fact]
    public void DynamicsChangeMidiVelocity()
    {
        var e = Create(". :4 5.1 5.1 5.1 5.1");
        e.MoveTo(0, 2, 1);
        e.SetDynamics(DynamicValue.PP);
        var velocities = Midi(Read(e.ExportGp())).Events.OfType<NoteOnEvent>()
            .Where(n => n.NoteKey == 69).OrderBy(n => n.Tick).Select(n => n.NoteVelocity).ToList();
        Assert.Equal(4, velocities.Count);
        Assert.Equal(velocities[0], velocities[1]);
        Assert.True(velocities[2] < velocities[1] - 20, string.Join(",", velocities));
        Assert.Equal(velocities[2], velocities[3]);
    }

    [Theory]
    [InlineData(GraceType.BeforeBeat)]
    [InlineData(GraceType.OnBeat)]
    public void GraceIsInsertedBeforeTheBeatAndKeepsTheBarFull(GraceType type)
    {
        var e = Create(". :4 3.1 7.1 r r | 8.1 r r r");
        var score = e.Score;
        e.MoveTo(0, 1, 1);
        Change(e, () => Assert.True(e.ToggleGrace(type)), () =>
        {
            Assert.Equal(type, e.CurrentBeat!.GraceType);
            Assert.Equal(5, e.CurrentNote!.Fret);
            Assert.Equal(7, e.CurrentVoice!.Beats[e.BeatIndex + 1].Notes[0].Fret);
            Assert.True(e.BarFill(out var used, out var total));
            Assert.Equal(total, used);
            Assert.Same(score, e.Score);
        });

        var midi = Midi(Read(e.ExportGp())).Events.OfType<NoteOnEvent>().ToList();
        var grace = midi.Single(n => n.NoteKey == 69);
        var main = midi.Single(n => n.NoteKey == 71);
        Assert.True(grace.Tick < main.Tick);
        if (type == GraceType.BeforeBeat)
        {
            Assert.Equal(960, main.Tick);
            Assert.True(grace.Tick > 0);
        }
        else
        {
            Assert.Equal(960, grace.Tick);
        }
    }

    [Fact]
    public void GraceToggleSwitchesTypeRemovesAndIsCopiedWithItsNote()
    {
        var e = Create(". :4 3.1 7.1 r r");
        e.MoveTo(0, 1, 1);
        e.ToggleGrace(GraceType.BeforeBeat);
        Change(e, () => Assert.True(e.ToggleGrace(GraceType.OnBeat)), () =>
            Assert.Equal(GraceType.OnBeat, e.CurrentBeat!.GraceType));
        Change(e, () => Assert.True(e.ToggleGrace(GraceType.OnBeat)), () =>
        {
            Assert.All(e.CurrentVoice!.Beats, b => Assert.Equal(GraceType.None, b.GraceType));
            Assert.Equal(4, e.CurrentVoice!.Beats.Count);
        });
    }

    [Fact]
    public void GraceAtBarStartDoesNotShortenThePreviousBar()
    {
        var e = Create(". :4 3.1 3.1 3.1 3.1 | 7.1 r r r");
        e.MoveTo(1, 0, 1);
        Change(e, () => e.ToggleGrace(GraceType.BeforeBeat), () =>
        {
            e.MoveTo(0, 0, 1);
            Assert.True(e.BarFill(out var used, out var total));
            Assert.Equal(total, used);
            e.MoveTo(1, 1, 1);
            Assert.True(e.BarFill(out used, out total));
            Assert.Equal(total, used);
            e.MoveTo(1, 0, 1);
        });
        var midi = Midi(Read(e.ExportGp())).Events.OfType<NoteOnEvent>().ToList();
        Assert.True(midi.Single(n => n.NoteKey == 69).Tick < 3840);
        Assert.Equal(3840, midi.Single(n => n.NoteKey == 71).Tick);
    }

    [Fact]
    public void GraceNeedsANoteAndLeavesWithItsMainBeat()
    {
        var e = Create(". :4 3.1 7.1 r r");
        e.MoveTo(0, 2, 1);
        Assert.NotNull(e.GraceWarning(GraceType.BeforeBeat));
        Assert.False(e.ToggleGrace(GraceType.BeforeBeat));
        Assert.False(e.CanUndo);

        e.MoveTo(0, 1, 1);
        e.ToggleGrace(GraceType.BeforeBeat);
        e.MoveTo(0, 2, 1);
        Change(e, e.DeleteBeat, () => Assert.Equal(GraceType.BeforeBeat, e.CurrentVoice!.Beats[1].GraceType));

        // Il tempo principale in fondo alla voce porta via l'abbellimento rimasto senza nota.
        e.MoveTo(0, 3, 1);
        e.DeleteBeat();
        e.MoveTo(0, 2, 1);
        Change(e, e.DeleteBeat, () => Assert.All(e.CurrentVoice!.Beats, b => Assert.Equal(GraceType.None, b.GraceType)));
    }
}
