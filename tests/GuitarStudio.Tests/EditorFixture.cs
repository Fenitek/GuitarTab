using System.Text.Json;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Model;
using GuitarStudio.Editing;

namespace GuitarStudio.Tests;

internal static class EditorFixture
{
    internal const string Tex = """
        \title "Prova editor"
        \tempo 110
        .
        :4 3.1 5.1 7.1 8.1 | 10.2 12.2 14.2 15.2 |
        1.3 2.3 3.3 4.3 | 5.4 6.4 7.4 8.4
        """;

    internal static TabEditor Create(string tex = Tex)
    {
        var settings = new Settings();
        var importer = new AlphaTexImporter();
        importer.InitFromString(tex, settings);
        var editor = new TabEditor(settings);
        editor.Attach(importer.ReadScore(), 0);
        AssertModel(editor.Score!);
        editor.Changed += () => AssertModel(editor.Score!);
        return editor;
    }

    internal static Score Read(byte[] bytes) => ScoreLoader.LoadScoreFromBytes(new Uint8Array(bytes), new Settings());

    // Compare musical content, never ZIP timestamps, generated IDs or cached layout values.
    internal static string Content(Score score) => JsonSerializer.Serialize(new
    {
        score.Title, score.Artist,
        Bars = score.MasterBars.Select(m => new
        {
            m.TimeSignatureNumerator, m.TimeSignatureDenominator, m.IsRepeatStart, m.RepeatCount,
            Section = m.Section?.Text,
            Tempos = m.TempoAutomations.Select(a => a.Value)
        }),
        Tracks = score.Tracks.Select(t => new
        {
            t.Name, t.PlaybackInfo.Program, t.PlaybackInfo.PrimaryChannel, t.PlaybackInfo.SecondaryChannel,
            Staves = t.Staves.Select(s => new
            {
                s.Tuning, s.Capo,
                Bars = s.Bars.Select(b => b.Voices.Select(v => v.Beats.Where(beat => !beat.IsEmpty).Select(beat => new
                {
                    beat.Duration, beat.Dots, Tuplet = beat.HasTuplet ? beat.TupletNumerator : 0,
                    Denominator = beat.HasTuplet ? beat.TupletDenominator : 0,
                    beat.IsEmpty,
                    Notes = beat.Notes.Select(n => new
                    {
                        n.String, n.Fret, n.IsHammerPullOrigin, n.IsTieDestination, n.SlideOutType,
                        n.IsPalmMute, n.IsDead
                    })
                })))
            })
        })
    });

    internal static void Change(TabEditor editor, Action action, Action verify)
    {
        var before = Content(editor.Score!);
        action();
        AssertModel(editor.Score!);
        verify();
        var after = Content(editor.Score!);
        Assert.NotEqual(before, after);
        Assert.True(editor.IsDirty);
        Assert.True(editor.CanUndo);
        editor.Undo();
        AssertModel(editor.Score!);
        Assert.Equal(before, Content(editor.Score!));
        Assert.True(editor.CanRedo);
        editor.Redo();
        AssertModel(editor.Score!);
        Assert.Equal(after, Content(editor.Score!));
        verify();
        var restored = Read(editor.ExportGp());
        AssertModel(restored);
        Assert.Equal(after, Content(restored));
    }

    internal static void AssertModel(Score score)
    {
        var bars = score.Tracks.SelectMany(t => t.Staves).SelectMany(s => s.Bars).ToList();
        var voices = bars.SelectMany(b => b.Voices).ToList();
        var beats = voices.SelectMany(v => v.Beats).ToList();
        Assert.Equal(bars.Count, bars.Select(b => b.Id).Distinct().Count());
        Assert.Equal(voices.Count, voices.Select(v => v.Id).Distinct().Count());
        Assert.Equal(beats.Count, beats.Select(b => b.Id).Distinct().Count());
        var notes = beats.SelectMany(b => b.Notes).ToList();
        Assert.Equal(notes.Count, notes.Select(n => n.Id).Distinct().Count());
        for (var i = 0; i < score.MasterBars.Count; i++)
        {
            var master = score.MasterBars[i];
            Assert.Equal(i, master.Index);
            Assert.Same(score, master.Score);
            Assert.Same(i > 0 ? score.MasterBars[i - 1] : null, master.PreviousMasterBar);
            Assert.Same(i + 1 < score.MasterBars.Count ? score.MasterBars[i + 1] : null, master.NextMasterBar);
            Assert.NotNull(master.RepeatGroup);
        }
        for (var t = 0; t < score.Tracks.Count; t++)
        {
            var track = score.Tracks[t];
            Assert.Equal(t, track.Index);
            Assert.Same(score, track.Score);
            for (var s = 0; s < track.Staves.Count; s++)
            {
                var staff = track.Staves[s];
                Assert.Equal(s, staff.Index);
                Assert.Same(track, staff.Track);
                Assert.Equal(score.MasterBars.Count, staff.Bars.Count);
                for (var b = 0; b < staff.Bars.Count; b++)
                {
                    var bar = staff.Bars[b];
                    Assert.Equal(b, bar.Index);
                    Assert.Same(staff, bar.Staff);
                    Assert.Same(score.MasterBars[b], bar.MasterBar);
                    Assert.Same(b > 0 ? staff.Bars[b - 1] : null, bar.PreviousBar);
                    Assert.Same(b + 1 < staff.Bars.Count ? staff.Bars[b + 1] : null, bar.NextBar);
                    for (var v = 0; v < bar.Voices.Count; v++)
                    {
                        var voice = bar.Voices[v];
                        Assert.Equal(v, voice.Index);
                        Assert.Same(bar, voice.Bar);
                        Assert.NotEmpty(voice.Beats);
                        for (var i = 0; i < voice.Beats.Count; i++)
                        {
                            var beat = voice.Beats[i];
                            Assert.Equal(i, beat.Index);
                            Assert.Same(voice, beat.Voice);
                            for (var n = 0; n < beat.Notes.Count; n++)
                            {
                                Assert.Equal(n, beat.Notes[n].Index);
                                Assert.Same(beat, beat.Notes[n].Beat);
                            }
                        }
                    }
                }
                var maxVoices = staff.Bars.Max(b => b.Voices.Count);
                for (var v = 0; v < maxVoices; v++)
                {
                    var chain = staff.Bars.Where(b => b.Voices.Count > v).SelectMany(b => b.Voices[v].Beats).ToList();
                    for (var i = 0; i < chain.Count; i++)
                    {
                        Assert.Same(i > 0 ? chain[i - 1] : null, chain[i].PreviousBeat);
                        Assert.Same(i + 1 < chain.Count ? chain[i + 1] : null, chain[i].NextBeat);
                    }
                }
            }
        }
    }
}
