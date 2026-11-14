using System.IO;
using System.Windows;
using GuitarStudio.Ui;

namespace GuitarStudio;

public partial class App : Application
{
    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuitarStudio", "crash.log");

    /// <summary>File passato sulla riga di comando (per esempio trascinando una tablatura sull'icona).</summary>
    public static string? StartupFile { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
            Dialogs.Error(null, "Si è verificato un errore:\n" + args.Exception.Message + "\n\nDettagli in: " + LogPath);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Log(ex);
        };
        StartupFile = e.Args.FirstOrDefault(File.Exists);
        base.OnStartup(e);
    }

    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{AppInfo.Version} {ex}\n\n");
        }
        catch
        {
            // Il log non deve mai far cadere il programma.
        }
    }
}
