namespace GuitarStudio.Editing;

/// <summary>Nomi delle note: italiani (Do Re Mi) o internazionali (C D E).</summary>
public enum ChordNotation { Italian, International }

/// <summary>Come scrivere le alterazioni dopo una trasposizione.</summary>
public enum Spelling { Sharps, Flats }

/// <summary>Nota (fondamentale o basso): lettera 0 = Do/C … 6 = Si/B, alterazione −2…+2.</summary>
public readonly record struct NoteName(int Letter, int Accidental)
{
    private static readonly int[] LetterPitch = [0, 2, 4, 5, 7, 9, 11];
    private static readonly NoteName[] SharpNames =
        [new(0, 0), new(0, 1), new(1, 0), new(1, 1), new(2, 0), new(3, 0), new(3, 1), new(4, 0), new(4, 1), new(5, 0), new(5, 1), new(6, 0)];
    private static readonly NoteName[] FlatNames =
        [new(0, 0), new(1, -1), new(1, 0), new(2, -1), new(2, 0), new(3, 0), new(4, -1), new(4, 0), new(5, -1), new(5, 0), new(6, -1), new(6, 0)];

    /// <summary>Classe di altezza 0–11 (Do = 0).</summary>
    public int Pitch => ((LetterPitch[Letter] + Accidental) % 12 + 12) % 12;

    public static NoteName FromPitch(int pitch, Spelling spelling) =>
        (spelling == Spelling.Flats ? FlatNames : SharpNames)[(pitch % 12 + 12) % 12];

    public NoteName Transpose(int semitones, Spelling spelling) => FromPitch(Pitch + semitones, spelling);
}

/// <summary>
/// Nome d'accordo scomposto: fondamentale, suffisso (m, 7, maj7, sus4…) e basso facoltativo
/// («Sol/Si»). Riconosce i nomi italiani e internazionali; ricorda quale notazione aveva.
/// </summary>
public sealed record ChordName(NoteName Root, string Suffix, NoteName? Bass, ChordNotation Notation)
{
    private static readonly string[] ItalianRoots = ["Do", "Re", "Mi", "Fa", "Sol", "La", "Si"];
    private static readonly string[] InternationalRoots = ["C", "D", "E", "F", "G", "A", "B"];

    /// <summary>Legge un nome d'accordo; null se non comincia con una nota (es. «N.C.», «%»).</summary>
    public static ChordName? Parse(string text)
    {
        text = text.Trim();
        if (ParseNote(text, 0, out var root, out var notation, out var end) is false) return null;
        var rest = text[end..];
        NoteName? bass = null;
        var slash = rest.LastIndexOf('/');
        if (slash >= 0 && ParseNote(rest, slash + 1, out var b, out _, out var bassEnd) && bassEnd == rest.Length)
        {
            bass = b;
            rest = rest[..slash];
        }
        return new ChordName(root, rest, bass, notation);
    }

    /// <summary>Nota all'inizio di <paramref name="text"/> da <paramref name="start"/>: italiana prima, poi internazionale.</summary>
    private static bool ParseNote(string text, int start, out NoteName note, out ChordNotation notation, out int end)
    {
        note = default;
        notation = ChordNotation.Italian;
        end = start;
        var letter = -1;
        for (var i = 0; i < ItalianRoots.Length && letter < 0; i++)
        {
            var name = ItalianRoots[i];
            if (string.Compare(text, start, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0) continue;
            // «Fadd9» e «Faug» sono Fa internazionale + add9/aug, non «Fa» + «dd9».
            if (i == 3 && char.IsUpper(text[start]) && text.Length > start + 2 && text[start + 1] == 'a' &&
                (string.Compare(text, start + 2, "dd", 0, 2, StringComparison.Ordinal) == 0 ||
                 string.Compare(text, start + 2, "ug", 0, 2, StringComparison.Ordinal) == 0))
                break;
            letter = i;
            end = start + name.Length;
        }
        if (letter < 0)
        {
            var en = start < text.Length ? Array.IndexOf(InternationalRoots, text[start].ToString()) : -1;
            if (en < 0) return false;
            letter = en;
            notation = ChordNotation.International;
            end = start + 1;
        }
        var accidental = 0;
        while (end < text.Length && Math.Abs(accidental) < 2)
        {
            var c = text[end];
            if (c is '#' or '♯' && accidental >= 0) accidental++;
            else if (c is 'b' or '♭' && accidental <= 0) accidental--;
            else break;
            end++;
        }
        note = new NoteName(letter, accidental);
        return true;
    }

    public static string NoteText(NoteName note, ChordNotation notation, bool symbols = false)
    {
        var name = notation == ChordNotation.Italian ? ItalianRoots[note.Letter] : InternationalRoots[note.Letter];
        var mark = note.Accidental > 0 ? (symbols ? "♯" : "#") : (symbols ? "♭" : "b");
        return name + string.Concat(Enumerable.Repeat(mark, Math.Abs(note.Accidental)));
    }

    /// <summary>Testo nella notazione data; <paramref name="symbols"/> usa ♯ e ♭ (lettura) invece di # e b.</summary>
    public string Format(ChordNotation notation, bool symbols = false) =>
        NoteText(Root, notation, symbols) + Suffix + (Bass is { } bass ? "/" + NoteText(bass, notation, symbols) : "");

    public override string ToString() => Format(Notation);

    public ChordName Transpose(int semitones, Spelling spelling) =>
        this with { Root = Root.Transpose(semitones, spelling), Bass = Bass?.Transpose(semitones, spelling) };

    /// <summary>Accordo (o tonalità) minore: suffisso che comincia con «m» ma non «maj».</summary>
    public bool IsMinor => Suffix.StartsWith('m') && !Suffix.StartsWith("maj", StringComparison.Ordinal);

    // ---------------------------------------------------------------- tonalità

    private static readonly NoteName[] MajorKeys =
        [new(0, 0), new(1, -1), new(1, 0), new(2, -1), new(2, 0), new(3, 0), new(3, 1), new(4, 0), new(5, -1), new(5, 0), new(6, -1), new(6, 0)];
    private static readonly NoteName[] MinorKeys =
        [new(0, 0), new(0, 1), new(1, 0), new(2, -1), new(2, 0), new(3, 0), new(3, 1), new(4, 0), new(4, 1), new(5, 0), new(6, -1), new(6, 0)];

    /// <summary>
    /// Tonalità dopo la trasposizione, scritta con meno alterazioni possibile
    /// (La♭ maggiore, non Sol♯; Do♯ minore, non Re♭).
    /// </summary>
    public ChordName TransposeKey(int semitones)
    {
        var pitch = ((Root.Pitch + semitones) % 12 + 12) % 12;
        return this with { Root = (IsMinor ? MinorKeys : MajorKeys)[pitch], Bass = null };
    }

    /// <summary>Le tonalità con i bemolle in chiave (Fa, Si♭, Re minore, Sol minore…) scrivono gli accordi con i bemolle.</summary>
    public Spelling KeySpelling()
    {
        if (Root.Accidental < 0) return Spelling.Flats;
        if (Root.Accidental > 0) return Spelling.Sharps;
        // Naturali con bemolle in chiave: Fa maggiore; Re, Sol, Do, Fa minore.
        return IsMinor ? Root.Letter is 1 or 4 or 0 or 3 ? Spelling.Flats : Spelling.Sharps
                       : Root.Letter == 3 ? Spelling.Flats : Spelling.Sharps;
    }

    /// <summary>Do maggiore e La minore non hanno alterazioni in chiave: decide la direzione.</summary>
    public bool IsNeutralKey => Root.Accidental == 0 && (IsMinor ? Root.Letter == 5 : Root.Letter == 0);
}
