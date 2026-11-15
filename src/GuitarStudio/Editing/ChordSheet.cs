using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace GuitarStudio.Editing;

/// <summary>Tipo di sezione: testo con gli accordi sopra le sillabe, oppure giro di soli accordi in battute.</summary>
public enum SheetSectionKind { Lyrics, Progression }

/// <summary>
/// Una sezione del foglio (Intro, Strofa, Ritornello…). Il corpo è il testo che l'utente scrive:
/// <list type="bullet">
/// <item>Testo: una riga per verso, accordi tra parentesi quadre nel punto della sillaba, es. «[Do]Nel mezzo del [Sol]cammin».</item>
/// <item>Giro: una riga per fila di battute, separate da «|», più accordi nella stessa battuta separati da spazi, es. «| Do | Sol Lam |».</item>
/// </list>
/// Il corpo resta il testo esatto: niente si perde tra scrittura, salvataggio e lettura.
/// </summary>
public sealed record SheetSection(string Name, SheetSectionKind Kind, string Body);

/// <summary>Foglio di accordi immutabile: ogni modifica produce un nuovo foglio (annulla = foglio precedente).</summary>
public sealed record ChordSheet(string Title, string Artist, string Key, int Capo, ImmutableList<SheetSection> Sections)
{
    public static ChordSheet Empty { get; } = new("", "", "", 0, ImmutableList<SheetSection>.Empty);

    /// <summary>Foglio nuovo: una strofa vuota, pronta da scrivere.</summary>
    public static ChordSheet New() => Empty with
    {
        Sections = ImmutableList.Create(new SheetSection("Strofa", SheetSectionKind.Lyrics, ""))
    };

    /// <summary>Confronto di contenuto (le liste immutabili si confrontano per riferimento).</summary>
    public bool SameContent(ChordSheet other) =>
        Title == other.Title && Artist == other.Artist && Key == other.Key && Capo == other.Capo &&
        Sections.SequenceEqual(other.Sections);
}

/// <summary>Accordo posizionato in una riga di testo: Position è l'indice del carattere del testo sotto l'accordo.</summary>
public sealed record ChordMark(int Position, string Name);

/// <summary>Riga di testo già separata: il testo da cantare e gli accordi sopra.</summary>
public sealed record LyricLine(string Text, IReadOnlyList<ChordMark> Chords);

/// <summary>Lettura e scrittura del corpo delle sezioni.</summary>
public static class ChordSheetText
{
    public const int MaxCapo = 12;

    private static readonly Regex InlineChord = new(@"\[([^\[\]\r\n]*)\]", RegexOptions.Compiled);

    /// <summary>Righe del corpo, senza le righe vuote finali.</summary>
    public static string[] Lines(string body)
    {
        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var count = lines.Length;
        while (count > 0 && lines[count - 1].Trim().Length == 0) count--;
        return lines[..count];
    }

    /// <summary>Corpo normalizzato: fine riga \n, niente spazi o righe vuote in coda.</summary>
    public static string Normalize(string body) =>
        string.Join("\n", Lines(body).Select(l => l.TrimEnd()));

    /// <summary>«[Do]Nel mezzo» → testo «Nel mezzo» con Do sul carattere 0. Le parentesi rotte restano testo.</summary>
    public static LyricLine ParseLyric(string line)
    {
        var text = new StringBuilder();
        var chords = new List<ChordMark>();
        var last = 0;
        foreach (Match m in InlineChord.Matches(line))
        {
            text.Append(line, last, m.Index - last);
            var name = m.Groups[1].Value.Trim();
            if (name.Length > 0) chords.Add(new ChordMark(text.Length, name));
            last = m.Index + m.Length;
        }
        text.Append(line, last, line.Length - last);
        return new LyricLine(text.ToString(), chords);
    }

    /// <summary>Inverso di <see cref="ParseLyric"/>.</summary>
    public static string FormatLyric(LyricLine line)
    {
        var sb = new StringBuilder();
        var last = 0;
        foreach (var chord in line.Chords.OrderBy(c => c.Position))
        {
            var at = Math.Clamp(chord.Position, last, line.Text.Length);
            sb.Append(line.Text, last, at - last).Append('[').Append(chord.Name).Append(']');
            last = at;
        }
        return sb.Append(line.Text, last, line.Text.Length - last).ToString();
    }

    /// <summary>«| Do | Sol Lam |» → battute [[Do], [Sol, Lam]]. Le battute vuote ai bordi si ignorano.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ParseRow(string line)
    {
        var bars = new List<IReadOnlyList<string>>();
        var parts = line.Split('|');
        for (var i = 0; i < parts.Length; i++)
        {
            var chords = parts[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // Le barre esterne «| … |» lasciano parti vuote all'inizio e alla fine: non sono battute.
            if (chords.Length == 0 && (i == 0 || i == parts.Length - 1)) continue;
            bars.Add(chords);
        }
        return bars;
    }

    public static string FormatRow(IEnumerable<IReadOnlyList<string>> bars) =>
        "| " + string.Join(" | ", bars.Select(b => string.Join(" ", b))) + " |";

    /// <summary>Tutti gli accordi della sezione nell'ordine in cui compaiono.</summary>
    public static IEnumerable<string> ChordNames(SheetSection section) => section.Kind == SheetSectionKind.Lyrics
        ? Lines(section.Body).SelectMany(l => ParseLyric(l).Chords.Select(c => c.Name))
        : Lines(section.Body).SelectMany(l => ParseRow(l).SelectMany(b => b));

    /// <summary>Sostituisce ogni nome d'accordo del corpo, senza toccare il resto del testo.</summary>
    public static string MapChords(SheetSection section, Func<string, string> map)
    {
        if (section.Kind == SheetSectionKind.Lyrics)
            return InlineChord.Replace(section.Body, m =>
            {
                var name = m.Groups[1].Value.Trim();
                return name.Length == 0 ? m.Value : "[" + map(name) + "]";
            });
        return Regex.Replace(section.Body, @"[^\s|]+", m => map(m.Value));
    }

    /// <summary>Un nome d'accordo valido: non vuoto, senza spazi, parentesi o barre di battuta.</summary>
    public static string? ChordNameProblem(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return "Scrivi il nome dell'accordo, ad esempio Do, Lam7 o G/B.";
        if (name.Length > 16) return "Il nome dell'accordo è troppo lungo (massimo 16 caratteri).";
        if (name.IndexOfAny(['[', ']', '|', ' ', '\t']) >= 0) return "Il nome dell'accordo non può contenere spazi, parentesi quadre o «|».";
        return null;
    }
}
