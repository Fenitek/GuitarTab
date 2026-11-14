using System.Reflection;

namespace GuitarStudio;

/// <summary>Informazioni sull'applicazione. La versione si cambia solo nel .csproj (vedi AGENTS.md).</summary>
public static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    public static string VersionLabel => "v" + Version;

    private static string ReadVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = v.IndexOf('+'); // toglie l'eventuale hash del commit
        return plus >= 0 ? v[..plus] : v;
    }
}
