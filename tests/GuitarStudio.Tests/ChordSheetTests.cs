using System.Collections.Immutable;
using GuitarStudio.Editing;
using GuitarStudio.Recovery;

namespace GuitarStudio.Tests;

public class ChordSheetTests
{
    private static ChordSheet Sample() => new("Canzone di prova", "Gruppo", "Sol", 0, ImmutableList.Create(
        new SheetSection("Intro", SheetSectionKind.Progression, "| Sol | Re | Mim Do |"),
        new SheetSection("Strofa", SheetSectionKind.Lyrics, "[Sol]Nel mezzo del [Re]cammin\ndi nostra [Mim]vita"),
        new SheetSection("Ritornello", SheetSectionKind.Lyrics, "[Do]Mi ritro[Sol]vai")));

    [Fact]
    public void Lyric_lines_split_text_and_chords_and_round_trip()
    {
        var line = ChordSheetText.ParseLyric("[Sol]Nel mezzo del [Re]cammin");
        Assert.Equal("Nel mezzo del cammin", line.Text);
        Assert.Equal(new[] { new ChordMark(0, "Sol"), new ChordMark(14, "Re") }, line.Chords);
        Assert.Equal("[Sol]Nel mezzo del [Re]cammin", ChordSheetText.FormatLyric(line));

        // Una parentesi senza chiusura resta testo; un accordo vuoto si ignora.
        var broken = ChordSheetText.ParseLyric("prova [Do senza fine []ok");
        Assert.Equal("prova [Do senza fine ok", broken.Text);
        Assert.Empty(broken.Chords);
    }

    [Fact]
    public void Progression_rows_are_bars_of_chords()
    {
        var bars = ChordSheetText.ParseRow("| Do | Sol Lam |  | Fa |");
        Assert.Equal(4, bars.Count);
        Assert.Equal(new[] { "Sol", "Lam" }, bars[1]);
        Assert.Empty(bars[2]); // battuta vuota in mezzo: resta, come pausa
        Assert.Equal("| Do | Sol Lam |  | Fa |", ChordSheetText.FormatRow(bars));
        Assert.Equal(new[] { "Do", "Sol" }, ChordSheetText.ParseRow("Do | Sol").Select(b => b.Single()));
    }

    [Fact]
    public void Typing_in_a_section_is_one_undo_step_until_EndTyping()
    {
        var editor = new ChordSheetEditor(Sample());
        editor.MarkSaved();
        foreach (var text in new[] { "[Do]Mi", "[Do]Mi r", "[Do]Mi ri" })
            Assert.True(editor.SetSectionBody(2, text, typing: true));
        Assert.True(editor.IsDirty);
        editor.EndTyping();
        editor.SetSectionBody(2, "[Do]Mi ri!", typing: true);

        Assert.True(editor.Undo());
        Assert.Equal("[Do]Mi ri", editor.Sheet.Sections[2].Body);
        Assert.True(editor.Undo());
        Assert.Equal("[Do]Mi ritro[Sol]vai", editor.Sheet.Sections[2].Body);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);

        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal("[Do]Mi ri!", editor.Sheet.Sections[2].Body);
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void Insert_chord_at_cursor_in_lyrics_and_progressions()
    {
        var editor = new ChordSheetEditor(Sample());
        // «di nostra [Mim]vita»: cursore davanti a «nostra».
        var body = editor.Sheet.Sections[1].Body;
        var caret = editor.InsertChord(1, body.IndexOf("nostra", StringComparison.Ordinal), "Lam7");
        Assert.Equal("[Sol]Nel mezzo del [Re]cammin\ndi [Lam7]nostra [Mim]vita", editor.Sheet.Sections[1].Body);
        Assert.Equal("[Lam7]", editor.Sheet.Sections[1].Body.Substring(caret - 6, 6));

        // Cursore dentro «[Mim]»: l'accordo va dopo la parentesi, non la spezza.
        var inside = editor.Sheet.Sections[1].Body.IndexOf("Mim", StringComparison.Ordinal) + 1;
        editor.InsertChord(1, inside, "Fa");
        Assert.Contains("[Mim][Fa]vita", editor.Sheet.Sections[1].Body);

        // Nel giro l'accordo diventa una parola con gli spazi giusti.
        var row = editor.Sheet.Sections[0].Body;
        caret = editor.InsertChord(0, row.IndexOf("|", 2, StringComparison.Ordinal), "Sol7");
        Assert.Equal("| Sol Sol7 | Re | Mim Do |", editor.Sheet.Sections[0].Body);
        Assert.Equal("| Sol Sol7", editor.Sheet.Sections[0].Body[..caret]);

        Assert.Equal(-1, editor.InsertChord(0, 0, "Do Re"));
        Assert.Equal(-1, editor.InsertChord(0, 0, "  "));

        // Tre inserimenti = tre passi di annulla.
        Assert.True(editor.Undo() && editor.Undo() && editor.Undo());
        Assert.True(editor.Sheet.SameContent(Sample()));
    }

    [Fact]
    public void Sections_add_rename_move_change_kind_and_undo()
    {
        var editor = new ChordSheetEditor(Sample());
        var index = editor.AddSection("Ponte", SheetSectionKind.Lyrics, after: 0);
        Assert.Equal(1, index);
        Assert.Equal("Ponte", editor.Sheet.Sections[1].Name);
        Assert.True(editor.RenameSection(1, "  Bridge  "));
        Assert.Equal("Bridge", editor.Sheet.Sections[1].Name);
        Assert.Equal(0, editor.MoveSection(1, -1));
        Assert.Equal("Bridge", editor.Sheet.Sections[0].Name);
        Assert.Equal(0, editor.MoveSection(0, -1)); // già in cima: nessun passo
        Assert.True(editor.SetSectionKind(3, SheetSectionKind.Progression));
        Assert.Equal("| Do | Sol |", editor.Sheet.Sections[3].Body);
        Assert.True(editor.SetSectionKind(3, SheetSectionKind.Lyrics));
        Assert.Equal("[Do] [Sol]", editor.Sheet.Sections[3].Body);
        Assert.True(editor.RemoveSection(0));
        Assert.True(editor.SetInfo(" Titolo ", "Artista", "Re", 99));
        Assert.Equal(("Titolo", ChordSheetText.MaxCapo), (editor.Sheet.Title, editor.Sheet.Capo));

        while (editor.Undo()) { }
        Assert.True(editor.Sheet.SameContent(Sample()));
        Assert.False(editor.IsDirty);
        while (editor.Redo()) { }
        Assert.Equal(new[] { "Intro", "Strofa", "Ritornello" }, editor.Sheet.Sections.Select(s => s.Name));
        Assert.Equal("Titolo", editor.Sheet.Title);
    }

    [Fact]
    public void Changes_notify_and_load_resets_history()
    {
        var editor = new ChordSheetEditor();
        var events = new List<bool>();
        editor.Changed += events.Add;
        editor.SetSectionBody(0, "[Do]ciao");
        editor.SetSectionBody(0, "[Do]ciao"); // uguale: nessun passo
        editor.Undo();
        editor.Load(Sample(), isDirty: true);
        Assert.Equal(new[] { false, true, true }, events);
        Assert.True(editor.IsDirty);
        Assert.False(editor.CanUndo);
        editor.MarkSaved();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void File_round_trip_is_readable_json()
    {
        var sheet = Sample() with { Capo = 3, Title = "Città è «bella»" };
        var json = ChordSheetFile.ToJson(sheet);
        Assert.Contains("\"formato\": \"GuitarStudio foglio di accordi\"", json);
        Assert.Contains("\"tipo\": \"giro\"", json);
        Assert.Contains("Città è «bella»", json); // accenti leggibili, non à
        Assert.Contains("\"[Sol]Nel mezzo del [Re]cammin\"", json);
        Assert.True(ChordSheetFile.FromJson(json).SameContent(sheet));

        var path = Path.Combine(Path.GetTempPath(), $"gs_test_{Guid.NewGuid():N}{ChordSheetFile.Extension}");
        try
        {
            ChordSheetFile.Write(path, sheet);
            Assert.True(ChordSheetFile.IsChordSheetPath(path));
            Assert.True(ChordSheetFile.Read(path).SameContent(sheet));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("{ non è json")]
    [InlineData("{\"formato\": \"altro\"}")]
    [InlineData("{\"formato\": \"GuitarStudio foglio di accordi\", \"versione\": 99}")]
    [InlineData("{\"formato\": \"GuitarStudio foglio di accordi\", \"versione\": 1, \"sezioni\": [{\"nome\": \"X\", \"tipo\": \"boh\"}]}")]
    public void Invalid_files_are_rejected_with_a_message(string json)
    {
        var ex = Assert.Throws<FormatException>(() => ChordSheetFile.FromJson(json));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Autosave_keeps_sheets_apart_from_tablatures()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gs_sheet_autosave_" + Guid.NewGuid().ToString("N"));
        try
        {
            var editor = new ChordSheetEditor(Sample());
            using (var store = new AutosaveStore(directory, ChordSheetFile.Extension))
            {
                Assert.False(store.SaveIfNeeded(editor)); // niente da salvare
                editor.SetSectionBody(2, "[Do]Cambiato");
                Assert.True(store.SaveIfNeeded(editor));
                Assert.Empty(AutosaveStore.FindRecoverable(directory, ChordSheetFile.Extension)); // sessione aperta
            }
            Assert.Empty(AutosaveStore.FindRecoverable(directory)); // le tablature non la vedono
            var found = AutosaveStore.FindRecoverable(directory, ChordSheetFile.Extension).ToList();
            try
            {
                Assert.Single(found);
                Assert.Equal("[Do]Cambiato", ChordSheetFile.Read(found[0].FilePath).Sections[2].Body);
            }
            finally { found.ForEach(f => f.Dispose()); }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Renaming_while_typing_is_one_undo_step_until_the_field_is_left()
    {
        var editor = new ChordSheetEditor(Sample());
        // Copia di recupero a metà nome, poi Ctrl+S, poi uscita dal campo: un solo passo.
        Assert.True(editor.RenameSection(1, "Ritor", typing: true));
        Assert.True(editor.RenameSection(1, "Ritornello", typing: true));
        Assert.False(editor.RenameSection(1, "Ritornello", typing: true));
        editor.EndTyping();
        Assert.Equal("Ritornello", editor.Sheet.Sections[1].Name);
        Assert.True(editor.Undo());
        Assert.Equal("Strofa", editor.Sheet.Sections[1].Name);
        Assert.False(editor.CanUndo);

        // Dopo EndTyping un nuovo nome è un passo a sé; il nome di un'altra sezione pure.
        Assert.True(editor.RenameSection(1, "Ponte", typing: true));
        editor.EndTyping();
        Assert.True(editor.RenameSection(1, "Coda", typing: true));
        Assert.True(editor.RenameSection(2, "Finale", typing: true));
        Assert.True(editor.Undo());
        Assert.Equal(("Coda", "Ritornello"), (editor.Sheet.Sections[1].Name, editor.Sheet.Sections[2].Name));
        Assert.True(editor.Undo());
        Assert.Equal("Ponte", editor.Sheet.Sections[1].Name);
    }
}
