namespace GuitarStudio.Editing;

/// <summary>
/// Diagrammi per chitarra in accordatura standard, nello stesso formato degli accordi della
/// tablatura (<see cref="ChordInfo"/>: tasti dalla corda più grave, null = non suonata).
/// Prima le posizioni aperte più comuni, poi i barrè a forma di Mi (6ª corda) e di La (5ª corda).
/// </summary>
public static class ChordShapes
{
    private const int X = -1;

    // Classe di altezza della fondamentale → forma aperta, per qualità.
    private static readonly Dictionary<(int Pitch, string Quality), int[]> Open = new()
    {
        [(0, "")] = [X, 3, 2, 0, 1, 0], [(0, "7")] = [X, 3, 2, 3, 1, 0], [(0, "maj7")] = [X, 3, 2, 0, 0, 0],
        [(2, "")] = [X, X, 0, 2, 3, 2], [(2, "m")] = [X, X, 0, 2, 3, 1], [(2, "7")] = [X, X, 0, 2, 1, 2],
        [(2, "m7")] = [X, X, 0, 2, 1, 1], [(2, "maj7")] = [X, X, 0, 2, 2, 2], [(2, "sus4")] = [X, X, 0, 2, 3, 3], [(2, "sus2")] = [X, X, 0, 2, 3, 0],
        [(4, "")] = [0, 2, 2, 1, 0, 0], [(4, "m")] = [0, 2, 2, 0, 0, 0], [(4, "7")] = [0, 2, 0, 1, 0, 0],
        [(4, "m7")] = [0, 2, 0, 0, 0, 0], [(4, "maj7")] = [0, 2, 1, 1, 0, 0], [(4, "sus4")] = [0, 2, 2, 2, 0, 0],
        [(5, "maj7")] = [X, X, 3, 2, 1, 0],
        [(7, "")] = [3, 2, 0, 0, 0, 3], [(7, "7")] = [3, 2, 0, 0, 0, 1], [(7, "maj7")] = [3, 2, 0, 0, 0, 2],
        [(9, "")] = [X, 0, 2, 2, 2, 0], [(9, "m")] = [X, 0, 2, 2, 1, 0], [(9, "7")] = [X, 0, 2, 0, 2, 0],
        [(9, "m7")] = [X, 0, 2, 0, 1, 0], [(9, "maj7")] = [X, 0, 2, 1, 2, 0], [(9, "sus4")] = [X, 0, 2, 2, 3, 0], [(9, "sus2")] = [X, 0, 2, 2, 0, 0],
        [(11, "7")] = [X, 2, 1, 2, 0, 2]
    };

    // Barrè: scostamenti dal tasto della fondamentale (X = corda muta).
    private static readonly Dictionary<string, int[]> EShape = new()
    {
        [""] = [0, 2, 2, 1, 0, 0], ["m"] = [0, 2, 2, 0, 0, 0], ["7"] = [0, 2, 0, 1, 0, 0],
        ["m7"] = [0, 2, 0, 0, 0, 0], ["maj7"] = [0, X, 1, 1, 0, X], ["sus4"] = [0, 2, 2, 2, 0, 0]
    };
    private static readonly Dictionary<string, int[]> AShape = new()
    {
        [""] = [X, 0, 2, 2, 2, 0], ["m"] = [X, 0, 2, 2, 1, 0], ["7"] = [X, 0, 2, 0, 2, 0], ["m7"] = [X, 0, 2, 0, 1, 0],
        ["maj7"] = [X, 0, 2, 1, 2, 0], ["sus4"] = [X, 0, 2, 2, 3, 0], ["sus2"] = [X, 0, 2, 2, 0, 0]
    };

    /// <summary>Qualità riconosciute; per le altre (dim, 9, add9…) il diagramma si definisce a mano.</summary>
    public static string? Quality(string suffix) => suffix switch
    {
        "" or "M" or "maj" => "",
        "m" or "min" or "-" => "m",
        "7" => "7",
        "m7" or "min7" or "-7" => "m7",
        "maj7" or "7M" or "Δ" or "M7" => "maj7",
        "sus4" or "sus" => "sus4",
        "sus2" => "sus2",
        _ => null
    };

    /// <summary>Diagramma proposto per il nome (il basso «/Si» non cambia la forma), oppure null.</summary>
    public static ChordInfo? For(string name)
    {
        if (ChordName.Parse(name) is not { } chord || Quality(chord.Suffix) is not { } quality) return null;
        var pitch = chord.Root.Pitch;
        if (Open.TryGetValue((pitch, quality), out var open)) return Info(name, open);
        // La forma con la fondamentale più bassa sul manico (tasti 1–12).
        var eFret = (pitch - 4 + 12) % 12;
        var aFret = (pitch - 9 + 12) % 12;
        if (eFret == 0) eFret = 12;
        if (aFret == 0) aFret = 12;
        var useA = AShape.ContainsKey(quality) && (!EShape.ContainsKey(quality) || aFret < eFret);
        var (shape, fret) = useA ? (AShape[quality], aFret) : (EShape[quality], eFret);
        return Info(name, shape.Select(o => o == X ? X : o + fret).ToArray());
    }

    private static ChordInfo Info(string name, int[] frets) =>
        new(name, true, frets.Select(f => f == X ? (int?)null : f).ToArray());
}
