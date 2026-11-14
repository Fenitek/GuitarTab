using AlphaTab.Model;

namespace GuitarStudio.Editing;

/// <summary>Suoni MIDI per chitarra/basso e nomi leggibili delle accordature.</summary>
public static class InstrumentCatalog
{
    public static readonly (int Program, string Name)[] Instruments =
    {
        (24, "Chitarra classica (nylon)"),
        (25, "Chitarra acustica (acciaio)"),
        (26, "Chitarra jazz"),
        (27, "Chitarra elettrica pulita"),
        (28, "Chitarra elettrica stoppata"),
        (29, "Chitarra overdrive"),
        (30, "Chitarra distorta"),
        (31, "Armonici di chitarra"),
        (32, "Basso acustico"),
        (33, "Basso elettrico (dita)"),
        (34, "Basso elettrico (plettro)"),
        (35, "Basso fretless"),
        (36, "Basso slap")
    };

    public static string InstrumentName(int program) =>
        Instruments.FirstOrDefault(i => i.Program == program).Name ?? $"Strumento MIDI {program}";

    public static bool IsBassTuning(IList<double> tuning) => tuning.Max() < 55;

    /// <summary>Accordature proposte per una traccia con quel numero di corde.</summary>
    public static List<Tuning> PresetsFor(int strings, bool bass)
    {
        var presets = Tuning.GetPresetsFor(strings)
            .Where(t => bass
                ? t.Name.StartsWith("Bass", StringComparison.OrdinalIgnoreCase) || strings == 5
                : !t.Name.StartsWith("Bass", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (presets.Count == 0 && Tuning.GetDefaultTuningFor(strings) is { } fallback) presets.Add(fallback);
        return presets;
    }

    public static string Describe(Tuning t) => Describe(t.Name, t.Tunings);

    public static string Describe(string name, IList<double> tunings)
    {
        var notes = string.Join(" ", tunings.Reverse().Select(v => Tuning.GetTextForTuning(v, false)));
        var label = name
            .Replace("Guitar ", "").Replace("Bass ", "").Replace(" Tuning", "")
            .Replace("Tune down ½ step", "½ tono sotto")
            .Replace("Tune down 1 step", "1 tono sotto")
            .Replace("Tune down 2 step", "2 toni sotto")
            .Replace("Double Dropped", "Doppio drop")
            .Replace("Dropped", "Drop")
            .Replace("variant", "variante");
        if (string.IsNullOrWhiteSpace(label)) label = "Personalizzata";
        return $"{label}  —  {notes}";
    }
}
