using System.IO.Compression;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Exporter;
using AlphaTab.Model;
using GuitarStudio.Diagnostics;
using GuitarStudio.Editing;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed partial class HistoryTests
{
    [Theory]
    [InlineData("Insert", 0)] [InlineData("Insert", 2)] [InlineData("Insert", 3)]
    [InlineData("Delete", 0)] [InlineData("Delete", 1)] [InlineData("Delete", 3)]
    [InlineData("MoveRight", 3)] [InlineData("Paste", 1)] [InlineData("PasteTwo", 1)]
    [InlineData("PasteReplace", 2)] [InlineData("PasteReplaceTwo", 2)]
    public void StructuralUndoAndRedoMatchFullGpSnapshot(string operation, int bar)
    {
        var e = RichScore();
        if (operation.StartsWith("Paste"))
        {
            e.MoveTo(0, 0, 1);
            if (operation.EndsWith("Two")) e.MoveTo(1, 7, 1, true);
            e.Copy();
            e.MoveTo(bar, 0, 1);
            if (operation.Contains("Replace"))
            {
                e.MoveTo(bar, 7, 1, true);
                e.MakeRest();
                e.MoveTo(bar, 0, 1);
            }
        }
        else e.MoveTo(bar, operation == "MoveRight" ? 7 : 0, 1);
        var score = e.Score;
        var before = e.ExportGp(); // Lo stesso snapshot completo usato dalla versione precedente.
        switch (operation)
        {
            case "Insert": e.InsertBarAfter(); break;
            case "Delete": e.DeleteBar(); break;
            case "MoveRight": e.MoveRight(); break;
            default: e.Paste(); break;
        }
        Assert.Same(score, e.Score);
        AssertModel(e.Score!);
        var after = e.ExportGp();
        for (var repeat = 0; repeat < 3; repeat++)
        {
            e.Undo(); AssertModel(e.Score!); Assert.Same(score, e.Score);
            AssertGpSnapshot(before, e.ExportGp());
            e.Redo(); AssertModel(e.Score!); Assert.Same(score, e.Score);
            AssertGpSnapshot(after, e.ExportGp());
        }
    }

    [Fact]
    public void StructuralHistoryPreservesLinksToRemovedBar()
    {
        var e = Create(". :4 3.1 r r r | 5.1 r r r | 7.1 r r r");
        e.ToggleEffect(NoteEffect.Slide);
        e.MoveTo(1, 0, 1);
        e.ToggleEffect(NoteEffect.HammerPull);
        var before = e.ExportGp();
        e.DeleteBar();
        var after = e.ExportGp();
        e.Undo(); AssertGpSnapshot(before, e.ExportGp()); AssertModel(e.Score!);
        e.Redo(); AssertGpSnapshot(after, e.ExportGp()); AssertModel(e.Score!);
    }

    [Fact]
    public void MixedStructuralAndLocalCommandsPreserveEveryGpState()
    {
        var e = RichScore();
        e.Copy();
        var states = new List<byte[]> { e.ExportGp() };
        Action[] actions = [e.InsertBarAfter, () => e.SetFret(12), e.Paste, e.DeleteBar,
            () => e.SetBarInfo(e.GetBarInfo() with { Numerator = 3 }), e.InsertBarAfter, () => e.SetFret(24)];
        foreach (var action in actions) { action(); AssertModel(e.Score!); states.Add(e.ExportGp()); }
        for (var i = actions.Length - 1; i >= 0; i--) { e.Undo(); AssertModel(e.Score!); AssertGpSnapshot(states[i], e.ExportGp()); }
        for (var i = 1; i <= actions.Length; i++) { e.Redo(); AssertModel(e.Score!); AssertGpSnapshot(states[i], e.ExportGp()); }
    }

    private static TabEditor RichScore()
    {
        var settings = new Settings();
        var score = PerformanceScore.Create(settings, 4, 4);
        var e = new TabEditor(settings);
        e.Attach(score, 0);
        e.SelectVoice(1); e.SetFret(19); e.SelectVoice(0);
        score.MasterBars[0].IsRepeatStart = true;
        score.MasterBars[1].Section = new Section { Text = "Ponte", Marker = "B" };
        score.MasterBars[1].TempoAutomations.Add(Automation.BuildTempoAutomation(false, 0, 155, 0, true));
        score.MasterBars[2].RepeatCount = 2;
        score.MasterBars[2].AlternateEndings = 1;
        for (var t = 0; t < score.Tracks.Count; t++)
        {
            var staff = score.Tracks[t].Staves[0];
            staff.Capo = t;
            staff.Bars[1].KeySignature = (KeySignature)(t + 1);
            staff.Bars[1].Voices[0].Beats[0].Text = $"Testo traccia {t}";
        }
        // Anche un secondo pentagramma nella stessa traccia deve seguire lo splice.
        var extra = new Staff { StringTuning = new Tuning("Chitarra", new double[] { 64, 59, 55, 50, 45, 40 }, true) };
        score.Tracks[1].AddStaff(extra);
        for (var b = 0; b < score.MasterBars.Count; b++)
        {
            var copy = new Bar(); extra.AddBar(copy);
            var voice = new Voice(); copy.AddVoice(voice);
            foreach (var beat in score.Tracks[1].Staves[0].Bars[b].Voices[0].Beats) voice.AddBeat(ModelCopy.CloneBeat(beat));
        }
        score.Finish(settings);
        e.Attach(score, 0);
        e.Changed += () => AssertModel(e.Score!);
        return e;
    }

    private static void AssertGpSnapshot(byte[] expected, byte[] actual)
    {
        // ZIP timestamp e ID interni cambiano anche con Full. Un identico roundtrip Full
        // rende confrontabili tutti i membri del .gp, non soltanto note e durate.
        var expectedMembers = CanonicalMembers(expected);
        var actualMembers = CanonicalMembers(actual);
        Assert.Equal(expectedMembers.Keys, actualMembers.Keys);
        foreach (var key in expectedMembers.Keys) Assert.Equal(expectedMembers[key], actualMembers[key]);
    }

    private static SortedDictionary<string, byte[]> CanonicalMembers(byte[] bytes)
    {
        var score = Read(bytes);
        var canonical = new Gp7Exporter().Export(score, new Settings());
        using var stream = new MemoryStream(canonical.Buffer.Raw, (int)canonical.ByteOffset, (int)canonical.Length);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var members = new SortedDictionary<string, byte[]>();
        foreach (var entry in archive.Entries)
        {
            using var member = entry.Open(); using var output = new MemoryStream(); member.CopyTo(output);
            members.Add(entry.FullName, output.ToArray());
        }
        return members;
    }
}
