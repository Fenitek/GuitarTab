using System.Text;
using AlphaTab;
using AlphaTab.Importer;
using AlphaTab.Model;

namespace GuitarStudio.Diagnostics;

/// <summary>Partitura deterministica condivisa dalle misure del modello e dell'app.</summary>
public static class PerformanceScore
{
    public static Score Create(Settings settings, int bars = 200, int tracks = 4)
    {
        var tex = new StringBuilder("\\title \"Prova prestazioni 200 battute\"\n\\tempo 120\n.\n");
        for (var track = 0; track < tracks; track++)
        {
            tex.AppendLine($"\\track \"Chitarra {track + 1}\"");
            for (var bar = 0; bar < bars; bar++)
            {
                if (bar > 0) tex.Append(" | ");
                tex.Append(":8 (3.1 5.2 5.3) 5.1 7.2 8.2 3.3 5.3 7.4 5.4");
            }
            tex.AppendLine();
        }
        var importer = new AlphaTexImporter();
        importer.InitFromString(tex.ToString(), settings);
        return importer.ReadScore();
    }
}
