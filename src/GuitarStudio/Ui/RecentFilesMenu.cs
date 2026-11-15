using System.IO;
using System.Windows.Controls;
using GuitarStudio.Editing;

namespace GuitarStudio.Ui;

/// <summary>Menu «Apri recente» condiviso da tablature e fogli di accordi: un solo elenco, il tipo a destra.</summary>
public static class RecentFilesMenu
{
    public static string KindLabel(string path) =>
        ChordSheetFile.IsChordSheetPath(path) ? "foglio di accordi" : "tablatura";

    /// <param name="open">Apre il file esistente (instradato per tipo).</param>
    /// <param name="notify">Messaggio quando un file non esiste più e viene tolto dall'elenco.</param>
    public static void Fill(MenuItem menu, Action<string> open, Action<string> notify)
    {
        menu.Items.Clear();
        var recent = AppSettings.Current.RecentFiles;
        menu.IsEnabled = recent.Count > 0;
        foreach (var path in recent.ToList())
        {
            var item = new MenuItem { Header = Path.GetFileName(path), ToolTip = path, InputGestureText = KindLabel(path) };
            item.Click += (_, _) =>
            {
                if (File.Exists(path))
                {
                    open(path);
                    return;
                }
                AppSettings.Current.RemoveRecent(path);
                notify($"«{Path.GetFileName(path)}» non si trova più: l'ho tolto dai file recenti.");
            };
            menu.Items.Add(item);
        }
    }
}
