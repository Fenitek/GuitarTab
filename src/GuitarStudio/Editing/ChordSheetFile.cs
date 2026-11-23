using System.IO;
using System.Collections.Immutable;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using GuitarStudio.Recovery;

namespace GuitarStudio.Editing;

/// <summary>
/// File «.accordi»: JSON in UTF-8, leggibile e modificabile a mano. Formato documentato in HANDOFF.md.
/// <code>
/// {
///   "formato": "GuitarStudio foglio di accordi",
///   "versione": 1,
///   "titolo": "…", "artista": "…", "tonalita": "Sol", "capotasto": 0,
///   "sezioni": [
///     { "nome": "Strofa", "tipo": "testo", "righe": [ "[Sol]Nel mezzo del [Re]cammin" ] },
///     { "nome": "Intro",  "tipo": "giro",  "righe": [ "| Sol | Re | Mim Do |" ] }
///   ]
/// }
/// </code>
/// </summary>
public static class ChordSheetFile
{
    public const string Extension = ".accordi";
    public const string FormatName = "GuitarStudio foglio di accordi";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Accenti e simboli restano leggibili nel file (non à).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private sealed class FileDto
    {
        [JsonPropertyName("formato")] public string? Format { get; set; }
        [JsonPropertyName("versione")] public int Version { get; set; }
        [JsonPropertyName("titolo")] public string? Title { get; set; }
        [JsonPropertyName("artista")] public string? Artist { get; set; }
        [JsonPropertyName("tonalita")] public string? Key { get; set; }
        [JsonPropertyName("capotasto")] public int Capo { get; set; }
        [JsonPropertyName("sezioni")] public List<SectionDto>? Sections { get; set; }
        // Facoltativo (v0.11): diagrammi scelti a mano, nome → tasti dalla corda più grave.
        [JsonPropertyName("diagrammi"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string>? Diagrams { get; set; }
    }

    private sealed class SectionDto
    {
        [JsonPropertyName("nome")] public string? Name { get; set; }
        [JsonPropertyName("tipo")] public string? Kind { get; set; }
        [JsonPropertyName("righe")] public List<string>? Lines { get; set; }
    }

    public static bool IsChordSheetPath(string path) =>
        string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);

    public static string ToJson(ChordSheet sheet) => JsonSerializer.Serialize(new FileDto
    {
        Format = FormatName,
        Version = FormatVersion,
        Title = sheet.Title,
        Artist = sheet.Artist,
        Key = sheet.Key,
        Capo = sheet.Capo,
        Sections = sheet.Sections.Select(s => new SectionDto
        {
            Name = s.Name,
            Kind = s.Kind == SheetSectionKind.Progression ? "giro" : "testo",
            Lines = ChordSheetText.Lines(s.Body).Select(l => l.TrimEnd()).ToList()
        }).ToList(),
        Diagrams = sheet.Diagrams.Count > 0 ? sheet.Diagrams.ToDictionary(d => d.Key, d => d.Value) : null
    }, Options) + "\n";

    public static byte[] ToBytes(ChordSheet sheet) => new UTF8Encoding(false).GetBytes(ToJson(sheet));

    /// <summary>Legge un foglio. Un file non valido produce <see cref="FormatException"/> con un messaggio in italiano.</summary>
    public static ChordSheet FromJson(string json)
    {
        FileDto? dto;
        try { dto = JsonSerializer.Deserialize<FileDto>(json, Options); }
        catch (JsonException ex) { throw new FormatException("Il file non è un foglio di accordi valido (JSON non leggibile).", ex); }
        if (dto == null || dto.Format != FormatName)
            throw new FormatException("Il file non è un foglio di accordi di GuitarStudio.");
        if (dto.Version > FormatVersion)
            throw new FormatException($"Il foglio è stato salvato da una versione più recente di GuitarStudio (formato {dto.Version}).");
        var sections = (dto.Sections ?? new()).Select(s => new SheetSection(
            string.IsNullOrWhiteSpace(s.Name) ? "Sezione" : s.Name.Trim(),
            s.Kind switch
            {
                "giro" => SheetSectionKind.Progression,
                "testo" or null => SheetSectionKind.Lyrics,
                _ => throw new FormatException($"Tipo di sezione sconosciuto: «{s.Kind}». Sono ammessi «testo» e «giro».")
            },
            ChordSheetText.Normalize(string.Join("\n", s.Lines ?? new()))));
        return new ChordSheet(dto.Title?.Trim() ?? "", dto.Artist?.Trim() ?? "", dto.Key?.Trim() ?? "",
            Math.Clamp(dto.Capo, 0, ChordSheetText.MaxCapo), sections.ToImmutableList())
        {
            Diagrams = (dto.Diagrams ?? new())
                .Where(d => !string.IsNullOrWhiteSpace(d.Key) && TabEditor.ParseFrets(d.Value ?? "", 6) != null)
                .ToImmutableSortedDictionary(d => d.Key.Trim(), d => d.Value.Trim())
        };
    }

    public static ChordSheet Read(string path) => FromJson(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>
    /// Scrive su un temporaneo univoco nella stessa cartella e sostituisce solo a scrittura completa:
    /// un errore non lascia un file a metà e non tocca altri file (nemmeno un «&lt;file&gt;.tmp» dell'utente).
    /// </summary>
    public static void Write(string path, ChordSheet sheet) => SafeFile.WriteAllBytes(path, ToBytes(sheet));
}
