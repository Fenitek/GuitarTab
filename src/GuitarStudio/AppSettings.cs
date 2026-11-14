using System.IO;
using System.Text.Json;

namespace GuitarStudio;

/// <summary>Preferenze dell'utente, salvate in %LOCALAPPDATA%\GuitarStudio\settings.json.</summary>
public sealed class AppSettings
{
    private const int MaxRecent = 10;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuitarStudio", "settings.json");

    public static AppSettings Current { get; } = Load();

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public double PaletteWidth { get; set; } = 226;
    public double InspectorWidth { get; set; } = 290;

    /// <summary>SoundFont scelto dall'utente; null = quello incluso.</summary>
    public string? SoundFontPath { get; set; }

    public List<string> RecentFiles { get; set; } = new();

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecent) RecentFiles.RemoveRange(MaxRecent, RecentFiles.Count - MaxRecent);
        Save();
    }

    public void RemoveRecent(string path)
    {
        if (RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0) Save();
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
