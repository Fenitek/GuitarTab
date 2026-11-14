using System.IO;
using GuitarStudio.Editing;

namespace GuitarStudio.Recovery;

/// <summary>Copia locale atomica. Il lease esclude dal recupero le sessioni ancora aperte.</summary>
public sealed class AutosaveStore : IDisposable
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuitarStudio", "autosave");

    private readonly FileStream _lease;
    private bool _disposed;
    public string FilePath { get; }

    public AutosaveStore(string? directory = null)
        : this(Path.Combine(directory ?? DefaultDirectory, $"{Guid.NewGuid():N}.gp"), createDirectory: true)
    {
    }

    private AutosaveStore(string path, bool createDirectory)
    {
        FilePath = path;
        if (createDirectory) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    /// <summary>Acquisisce una copia alla volta, dalla più recente. Il chiamante deve disporla.</summary>
    public static IEnumerable<AutosaveStore> FindRecoverable(string? directory = null)
    {
        directory ??= DefaultDirectory;
        if (!Directory.Exists(directory)) yield break;
        foreach (var path in Directory.GetFiles(directory, "*.gp").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            AutosaveStore candidate;
            try { candidate = new AutosaveStore(path, createDirectory: false); }
            catch (IOException) { continue; } // Il file appartiene a una sessione ancora aperta.
            if (File.Exists(path)) yield return candidate;
            else candidate.Dispose();
        }
    }

    /// <summary>Non marca il documento come salvato e non ne modifica il percorso originale.</summary>
    public bool SaveIfNeeded(TabEditor editor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (editor.Score == null || !editor.IsDirty) return false;
        var bytes = editor.ExportGp();
        var temporary = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            // Stesso volume: una scrittura fallita lascia intatta la copia precedente.
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return true;
    }

    public void Delete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        File.Delete(FilePath);
    }

    /// <summary>Rilascia la sessione; la copia rimane recuperabile finché non si chiama Delete.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lease.Dispose();
        try { File.Delete(FilePath + ".lock"); }
        catch (IOException) { /* Un'altra istanza ha già acquisito il lease. */ }
        catch (UnauthorizedAccessException) { /* Non impedire la chiusura. */ }
    }
}
