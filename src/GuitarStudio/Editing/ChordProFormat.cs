using System.IO;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace GuitarStudio.Editing;

/// <summary>
/// ChordPro (https://www.chordpro.org), formato di testo aperto per testi con accordi.
/// Esportazione: direttive title/artist/key/capo, {define} per i diagrammi scelti a mano,
/// una sezione {start_of_verse|chorus|bridge|grid: label="…"} per ogni sezione del foglio.
/// Importazione: le stesse direttive (anche abbreviate), {comment} come titolo di sezione,
/// righe fuori dalle sezioni in una sezione «Testo», griglie senza i punti di riempimento.
/// </summary>
public static class ChordProFormat
{
    public static readonly string[] Extensions = [".cho", ".chordpro", ".chopro", ".crd", ".pro"];

    public static bool IsChordProPath(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- esportazione

    public static string Export(ChordSheet sheet)
    {
        var sb = new StringBuilder();
        void Line(string text = "") => sb.Append(text).Append('\n');
        if (sheet.Title.Length > 0) Line($"{{title: {sheet.Title}}}");
        if (sheet.Artist.Length > 0) Line($"{{artist: {sheet.Artist}}}");
        if (sheet.Key.Length > 0) Line($"{{key: {sheet.Key}}}");
        if (sheet.Capo > 0) Line($"{{capo: {sheet.Capo}}}");
        foreach (var (name, frets) in sheet.Diagrams)
            Line($"{{define: {name} base-fret 1 frets {frets}}}");
        foreach (var section in sheet.Sections)
        {
            var kind = KindOf(section);
            Line();
            Line($"{{start_of_{kind}: label=\"{section.Name.Replace("\"", "'")}\"}}");
            foreach (var line in ChordSheetText.Lines(section.Body)) Line(line.TrimEnd());
            Line($"{{end_of_{kind}}}");
        }
        return sb.ToString();
    }

    private static string KindOf(SheetSection section)
    {
        if (section.Kind == SheetSectionKind.Progression) return "grid";
        var name = section.Name.ToLowerInvariant();
        if (name.StartsWith("ritornello") || name.StartsWith("chorus") || name.StartsWith("refrain")) return "chorus";
        if (name.StartsWith("ponte") || name.StartsWith("bridge")) return "bridge";
        return "verse";
    }

    // ---------------------------------------------------------------- importazione

    private static readonly Regex Directive = new(@"^\{\s*([A-Za-z_\-]+)\s*(?::\s*(.*?))?\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex Label = new("label\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex Define = new(@"^(\S+)\s+base-fret\s+(\d+)\s+frets\s+(.+?)(?:\s+fingers\b.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Legge un testo ChordPro. Le direttive sconosciute si ignorano.</summary>
    public static ChordSheet Import(string text)
    {
        string title = "", artist = "", subtitle = "", key = "";
        var capo = 0;
        var diagrams = ImmutableSortedDictionary.CreateBuilder<string, string>();
        var sections = new List<SheetSection>();
        string? name = null;
        var kind = SheetSectionKind.Lyrics;
        var explicitSection = false;
        var lines = new List<string>();

        void Close()
        {
            if (name != null && (explicitSection || lines.Any(l => l.Trim().Length > 0)))
                sections.Add(new SheetSection(name, kind, ChordSheetText.Normalize(string.Join("\n", explicitSection ? lines : TrimLeading(lines)))));
            name = null;
            lines.Clear();
            explicitSection = false;
        }
        void Open(string sectionName, SheetSectionKind sectionKind, bool isExplicit)
        {
            Close();
            name = sectionName.Length > 40 ? sectionName[..40] : sectionName;
            kind = sectionKind;
            explicitSection = isExplicit;
        }

        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith('#')) continue; // commento ChordPro
            var match = Directive.Match(line.Trim());
            if (!match.Success)
            {
                if (name == null)
                {
                    if (line.Trim().Length == 0) continue;
                    Open("Testo", SheetSectionKind.Lyrics, false);
                }
                lines.Add(kind == SheetSectionKind.Progression ? CleanGridLine(line) : line);
                continue;
            }

            var directive = match.Groups[1].Value.ToLowerInvariant();
            var value = match.Groups[2].Value.Trim();
            var label = Label.Match(value) is { Success: true } l ? l.Groups[1].Value.Trim() : value;
            switch (directive)
            {
                case "title" or "t": title = value; break;
                case "subtitle" or "st": subtitle = value; break;
                case "artist": artist = value; break;
                case "key": key = value; break;
                case "capo": capo = int.TryParse(value, out var c) ? Math.Clamp(c, 0, ChordSheetText.MaxCapo) : 0; break;
                case "define":
                    if (ParseDefine(value) is { } defined) diagrams[defined.Name] = defined.Frets;
                    break;
                case "start_of_verse" or "sov": Open(Or(label, "Strofa"), SheetSectionKind.Lyrics, true); break;
                case "start_of_chorus" or "soc": Open(Or(label, "Ritornello"), SheetSectionKind.Lyrics, true); break;
                case "start_of_bridge" or "sob": Open(Or(label, "Ponte"), SheetSectionKind.Lyrics, true); break;
                case "start_of_grid" or "sog": Open(Or(label, "Giro"), SheetSectionKind.Progression, true); break;
                case "start_of_tab" or "sot": Open(Or(label, "Tablatura"), SheetSectionKind.Lyrics, true); break;
                case "comment" or "c" or "comment_italic" or "ci" or "comment_box" or "cb":
                    // Fuori da una sezione dichiarata, «{c: Ritornello}» fa da titolo della parte seguente.
                    if (!explicitSection && value.Length > 0) Open(value, SheetSectionKind.Lyrics, false);
                    break;
                default:
                    if (directive.StartsWith("start_of_")) Open(Or(label, Capitalize(directive["start_of_".Length..])), SheetSectionKind.Lyrics, true);
                    else if (directive.StartsWith("end_of_") || directive is "eov" or "eoc" or "eob" or "eog" or "eot") Close();
                    break;
            }
        }
        Close();
        if (artist.Length == 0) artist = subtitle;
        return new ChordSheet(title, artist, key, capo, sections.ToImmutableList()) { Diagrams = diagrams.ToImmutable() };
    }

    private static string Or(string value, string fallback) => value.Length > 0 ? value : fallback;

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].Replace('_', ' ');

    private static IEnumerable<string> TrimLeading(List<string> lines) => lines.SkipWhile(l => l.Trim().Length == 0);

    /// <summary>Griglia ChordPro → giro: «||», «|:» e «:|» diventano «|», i punti di riempimento spariscono.</summary>
    private static string CleanGridLine(string line)
    {
        line = Regex.Replace(line, @"\|\|+|\|:|:\||\|\.", "|");
        var tokens = Regex.Split(line, @"(\|)").SelectMany(part => part == "|" ? new[] { "|" } :
            part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t is not "." and not "/" and not "~"));
        var sb = new StringBuilder();
        foreach (var token in tokens) sb.Append(sb.Length > 0 ? " " : "").Append(token);
        return sb.ToString();
    }

    /// <summary>«Am base-fret 1 frets x 0 2 2 1 0» → tasti assoluti dalla corda più grave.</summary>
    private static (string Name, string Frets)? ParseDefine(string value)
    {
        var m = Define.Match(value.Trim());
        if (!m.Success) return null;
        var baseFret = Math.Max(1, int.Parse(m.Groups[2].Value));
        var parts = m.Groups[3].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) return null;
        var frets = new int?[6];
        for (var i = 0; i < 6; i++)
        {
            if (parts[i] is "x" or "X" or "-1" or "N") continue;
            if (!int.TryParse(parts[i], out var f) || f < 0) return null;
            frets[i] = f == 0 ? 0 : f + baseFret - 1;
        }
        return (m.Groups[1].Value, TabEditor.FormatFrets(frets));
    }
}
