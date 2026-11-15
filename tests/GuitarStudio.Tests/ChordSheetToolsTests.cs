using System.Collections.Immutable;
using GuitarStudio.Editing;

namespace GuitarStudio.Tests;

public class ChordSheetToolsTests
{
    private static ChordSheet Sheet(string key, params SheetSection[] sections) =>
        new("Prova", "", key, 0, sections.ToImmutableList());

    private static SheetSection Lyrics(string body) => new("Strofa", SheetSectionKind.Lyrics, body);
    private static SheetSection Row(string body) => new("Giro", SheetSectionKind.Progression, body);

    [Theory]
    [InlineData("Do", 0, 0, "", null, ChordNotation.Italian)]
    [InlineData("Sol#m7", 4, 1, "m7", null, ChordNotation.Italian)]
    [InlineData("Sib", 6, -1, "", null, ChordNotation.Italian)]
    [InlineData("La♭maj7", 5, -1, "maj7", null, ChordNotation.Italian)]
    [InlineData("Sol/Si", 4, 0, "", 6, ChordNotation.Italian)]
    [InlineData("F#m", 3, 1, "m", null, ChordNotation.International)]
    [InlineData("Bb7", 6, -1, "7", null, ChordNotation.International)]
    [InlineData("Fadd9", 3, 0, "add9", null, ChordNotation.International)]
    [InlineData("C6/9", 0, 0, "6/9", null, ChordNotation.International)]
    [InlineData("D/F#", 1, 0, "", 3, ChordNotation.International)]
    public void Names_are_parsed_in_both_notations(string text, int letter, int accidental, string suffix, int? bassLetter, ChordNotation notation)
    {
        var chord = ChordName.Parse(text)!;
        Assert.Equal(new NoteName(letter, accidental), chord.Root);
        Assert.Equal(suffix, chord.Suffix);
        Assert.Equal(bassLetter, chord.Bass?.Letter);
        Assert.Equal(notation, chord.Notation);
        Assert.Equal(text.Replace("♭", "b"), chord.ToString());
    }

    [Theory]
    [InlineData("N.C.")]
    [InlineData("%")]
    [InlineData("x")]
    [InlineData("")]
    public void Non_chords_are_not_parsed(string text) => Assert.Null(ChordName.Parse(text));

    [Fact]
    public void Notation_and_symbols()
    {
        var chord = ChordName.Parse("Sol#m7/Re#")!;
        Assert.Equal("G#m7/D#", chord.Format(ChordNotation.International));
        Assert.Equal("Sol♯m7/Re♯", chord.Format(ChordNotation.Italian, symbols: true));
        Assert.Equal("Sib", ChordName.Parse("Bb")!.Format(ChordNotation.Italian));
    }

    [Fact]
    public void Without_a_key_up_uses_sharps_and_down_uses_flats()
    {
        var editor = new ChordSheetEditor(Sheet("", Lyrics("[Mi]uno [Do]due")));
        Assert.True(editor.Transpose(+4));
        Assert.Equal("[Sol#]uno [Mi]due", editor.Sheet.Sections[0].Body);   // Mi +4 = Sol♯

        editor = new ChordSheetEditor(Sheet("", Lyrics("[Do]uno [Mi]due")));
        Assert.True(editor.Transpose(-4));
        Assert.Equal("[Lab]uno [Do]due", editor.Sheet.Sections[0].Body);   // Do −4 = La♭, stessa altezza di Sol♯
    }

    [Fact]
    public void The_key_decides_between_G_sharp_and_A_flat()
    {
        // Mi maggiore +4 = La♭ maggiore (4 bemolle, non Sol♯ maggiore con 8 diesis): l'accordo di Mi diventa La♭.
        var major = new ChordSheetEditor(Sheet("Mi", Lyrics("[Mi]uno [La]due [Si7]tre")));
        Assert.True(major.Transpose(4));
        Assert.Equal("Lab", major.Sheet.Key);
        Assert.Equal("[Lab]uno [Reb]due [Mib7]tre", major.Sheet.Sections[0].Body);

        // Mi minore +4 = Sol♯ minore (5 diesis): lo stesso Mi diventa Sol♯.
        var minor = new ChordSheetEditor(Sheet("Mim", Lyrics("[Mim]uno [Mi]due")));
        Assert.True(minor.Transpose(4));
        Assert.Equal("Sol#m", minor.Sheet.Key);
        Assert.Equal("[Sol#m]uno [Sol#]due", minor.Sheet.Sections[0].Body);

        // In notazione internazionale e nei giri; il basso segue; N.C. resta.
        var en = new ChordSheetEditor(Sheet("G", Row("| G | D/F# | Em N.C. |")));
        Assert.True(en.Transpose(-2));
        Assert.Equal("F", en.Sheet.Key);
        Assert.Equal("| F | C/E | Dm N.C. |", en.Sheet.Sections[0].Body);
        Assert.True(en.Transpose(+1));
        Assert.Equal("F#", en.Sheet.Key); // 6 alterazioni in entrambi i casi: si sceglie Fa♯ maggiore
        Assert.Equal("| F# | C#/F | D#m N.C. |", en.Sheet.Sections[0].Body);

        // Annulla riporta tutto com'era.
        Assert.True(en.Undo() && en.Undo());
        Assert.Equal("| G | D/F# | Em N.C. |", en.Sheet.Sections[0].Body);
        Assert.Equal("G", en.Sheet.Key);
        Assert.False(en.Transpose(12));
    }

    [Fact]
    public void Capo_keeps_the_sound_by_moving_the_shapes()
    {
        var editor = new ChordSheetEditor(Sheet("La", Lyrics("[La]uno [Re]due [Mi7]tre")));
        Assert.True(editor.SetCapo(2, keepSound: true));
        Assert.Equal(2, editor.Sheet.Capo);
        Assert.Equal("La", editor.Sheet.Key); // la tonalità reale non cambia
        Assert.Equal("[Sol]uno [Do]due [Re7]tre", editor.Sheet.Sections[0].Body);
        Assert.True(editor.SetCapo(0, keepSound: true));
        Assert.Equal("[La]uno [Re]due [Mi7]tre", editor.Sheet.Sections[0].Body);
        Assert.True(editor.SetCapo(3, keepSound: false));
        Assert.Equal("[La]uno [Re]due [Mi7]tre", editor.Sheet.Sections[0].Body);
        Assert.True(editor.Undo() && editor.Undo() && editor.Undo());
        Assert.Equal(0, editor.Sheet.Capo);
        Assert.Equal("[La]uno [Re]due [Mi7]tre", editor.Sheet.Sections[0].Body);
    }

    [Fact]
    public void Notation_conversion_rewrites_chords_and_key()
    {
        var editor = new ChordSheetEditor(Sheet("Sol", Lyrics("[Sol]uno [Mim7]due [Re/Fa#]tre"), Row("| Do | Lam N.C. |")));
        Assert.Equal(ChordNotation.Italian, editor.DominantNotation());
        Assert.True(editor.ConvertNotation(ChordNotation.International));
        Assert.Equal("G", editor.Sheet.Key);
        Assert.Equal("[G]uno [Em7]due [D/F#]tre", editor.Sheet.Sections[0].Body);
        Assert.Equal("| C | Am N.C. |", editor.Sheet.Sections[1].Body);
        Assert.Equal(ChordNotation.International, editor.DominantNotation());
        Assert.False(editor.ConvertNotation(ChordNotation.International));
        Assert.True(editor.Undo());
        Assert.Equal("[Sol]uno [Mim7]due [Re/Fa#]tre", editor.Sheet.Sections[0].Body);
    }

    [Theory]
    [InlineData("Do", "x 3 2 0 1 0")]
    [InlineData("C", "x 3 2 0 1 0")]
    [InlineData("Lam", "x 0 2 2 1 0")]
    [InlineData("Sol/Si", "3 2 0 0 0 3")]
    [InlineData("Fa", "1 3 3 2 1 1")]
    [InlineData("Fa#m", "2 4 4 2 2 2")]
    [InlineData("Sib", "x 1 3 3 3 1")]
    [InlineData("Do#m7", "x 4 6 4 5 4")]
    [InlineData("Bm", "x 2 4 4 3 2")]
    public void Diagrams_use_open_shapes_then_barre(string name, string frets)
    {
        var info = ChordShapes.For(name)!;
        Assert.Equal(frets, TabEditor.FormatFrets(info.FretsLowToHigh));
        Assert.Equal(name, info.Name);
        Assert.True(info.ShowDiagram);
    }

    [Fact]
    public void Custom_diagrams_override_and_are_saved()
    {
        Assert.Null(ChordShapes.For("Dodim"));
        var editor = new ChordSheetEditor(Sheet("", Lyrics("[Do]uno [Dodim]due [Do]tre [N.C.]")));
        Assert.Equal(new[] { "Do", "Dodim" }, editor.Sheet.UsedChords());
        Assert.True(editor.SetDiagram("Dodim", TabEditor.ParseFrets("x 3 4 5 4 x", 6)));
        Assert.Equal("x 3 4 5 4 x", TabEditor.FormatFrets(editor.Sheet.DiagramFor("Dodim")!.FretsLowToHigh));
        Assert.True(editor.SetDiagram("Do", TabEditor.ParseFrets("8 10 10 9 8 8", 6)));
        Assert.Equal("8 10 10 9 8 8", TabEditor.FormatFrets(editor.Sheet.DiagramFor("Do")!.FretsLowToHigh));

        var json = ChordSheetFile.ToJson(editor.Sheet);
        Assert.Contains("\"diagrammi\"", json);
        Assert.True(ChordSheetFile.FromJson(json).SameContent(editor.Sheet));

        Assert.True(editor.SetDiagram("Do", null)); // torna alla forma proposta
        Assert.Equal("x 3 2 0 1 0", TabEditor.FormatFrets(editor.Sheet.DiagramFor("Do")!.FretsLowToHigh));
        Assert.True(editor.Undo());
        Assert.Equal("8 10 10 9 8 8", editor.Sheet.Diagrams["Do"]);
    }

    private static ChordSheet Song() => new ChordSheet("Canzone", "Gruppo", "Sol", 2, ImmutableList.Create(
        new SheetSection("Intro", SheetSectionKind.Progression, "| Sol | Re | Mim Do |\n| Sol Re/Fa# | Mim |"),
        new SheetSection("Strofa 1", SheetSectionKind.Lyrics, "[Sol]Nel mezzo del [Re]cammin\n\ndi nostra [Mim]vita"),
        new SheetSection("Ritornello", SheetSectionKind.Lyrics, "[Do]Mi ritro[Sol]vai"),
        new SheetSection("Ponte", SheetSectionKind.Lyrics, "[Lam7]per una selva"),
        new SheetSection("Finale", SheetSectionKind.Lyrics, "")))
    { Diagrams = ImmutableSortedDictionary<string, string>.Empty.Add("Lam7", "x 0 2 0 1 3") };

    [Fact]
    public void ChordPro_round_trip_keeps_everything()
    {
        var sheet = Song();
        var text = ChordProFormat.Export(sheet);
        Assert.Contains("{title: Canzone}", text);
        Assert.Contains("{capo: 2}", text);
        Assert.Contains("{define: Lam7 base-fret 1 frets x 0 2 0 1 3}", text);
        Assert.Contains("{start_of_grid: label=\"Intro\"}", text);
        Assert.Contains("{start_of_chorus: label=\"Ritornello\"}", text);
        Assert.Contains("{start_of_bridge: label=\"Ponte\"}", text);
        Assert.Contains("[Sol]Nel mezzo del [Re]cammin", text);
        var back = ChordProFormat.Import(text);
        Assert.True(back.SameContent(sheet), ChordSheetFile.ToJson(back));
        Assert.Equal(text, ChordProFormat.Export(back));
    }

    [Fact]
    public void ChordPro_from_other_programs()
    {
        const string text = """
            # file d'esempio
            {t: Una canzone}
            {st: Un artista}
            {key: G}
            {define: G base-fret 3 frets 1 x x x x 1}

            [G]Prima riga senza sezione
            {c: Chorus}
            [C]Ritornello [G]qui
            {soc}
            [D]Dentro il chorus
            {eoc}
            {start_of_grid}
            || G . . . | D . . . :|
            {end_of_grid}
            {start_of_outro: label="Coda"}
            [Em]fine
            {end_of_outro}
            """;
        var sheet = ChordProFormat.Import(text);
        Assert.Equal(("Una canzone", "Un artista", "G", 0), (sheet.Title, sheet.Artist, sheet.Key, sheet.Capo));
        Assert.Equal(new[] { "Testo", "Chorus", "Ritornello", "Giro", "Coda" }, sheet.Sections.Select(s => s.Name));
        Assert.Equal("[G]Prima riga senza sezione", sheet.Sections[0].Body);
        Assert.Equal("[C]Ritornello [G]qui", sheet.Sections[1].Body);
        Assert.Equal(SheetSectionKind.Progression, sheet.Sections[3].Kind);
        Assert.Equal("| G | D |", sheet.Sections[3].Body);
        Assert.Equal(new[] { new[] { "G" }, new[] { "D" } }, ChordSheetText.ParseRow(sheet.Sections[3].Body));
        Assert.Equal("3 x x x x 3", sheet.Diagrams["G"]); // base-fret 3: tasti assoluti
    }
}
