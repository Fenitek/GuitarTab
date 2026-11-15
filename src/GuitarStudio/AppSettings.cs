using System.IO;
using System.Text.Json;

namespace GuitarStudio;

/// <summary>Preferenze dell'utente, salvate in %LOCALAPPDATA%\GuitarStudio\settings.json.</summary>
public sealed class AppSettings
{
    private const int MaxRecent = 10;

    private static readonly string FilePath = Path.Combine(AppPaths.DataDirectory, "settings.json");

    public static AppSettings Current { get; } = Load();

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public double PaletteWidth { get; set; } = 190;
    public double InspectorWidth { get; set; } = 250;
    public double FretboardHeight { get; set; } = 162;
    public double TracksHeight { get; set; } = 34;

    /// <summary>SoundFont scelto dall'utente; null = quello incluso.</summary>
    public string? SoundFontPath { get; set; }

    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Sezioni della tavolozza chiuse dall'utente (per titolo, es. «DINAMICA»).</summary>
    public List<string> CollapsedPaletteSections { get; set; } = new();

    /// <summary>Foglio di accordi: diagrammi degli accordi usati in cima al foglio.</summary>
    public bool ShowChordDiagrams { get; set; } = true;

    /// <summary>Foglio di accordi: cambiando il capotasto gli accordi diventano le forme da suonare.</summary>
    public bool CapoKeepsSound { get; set; } = true;

    /// <summary>L'elenco dei recenti è cambiato: tablature e fogli di accordi aggiornano i loro menu.</summary>
    public static event Action? RecentFilesChanged;

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecent) RecentFiles.RemoveRange(MaxRecent, RecentFiles.Count - MaxRecent);
        Save();
        RecentFilesChanged?.Invoke();
    }

    public void RemoveRecent(string path)
    {
        if (RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) == 0) return;
        Save();
        RecentFilesChanged?.Invoke();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            App.Log(ex); // preferenze illeggibili: si riparte da quelle predefinite
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }
}
