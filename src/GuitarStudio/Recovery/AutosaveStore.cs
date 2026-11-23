using System.IO;
using GuitarStudio.Editing;

namespace GuitarStudio.Recovery;

/// <summary>Copia locale atomica. Il lease esclude dal recupero le sessioni ancora aperte.</summary>
public sealed class AutosaveStore : IDisposable
{
    public static string DefaultDirectory => Path.Combine(AppPaths.DataDirectory, "autosave");

    private readonly FileStream _lease;
    private bool _disposed;
    public string FilePath { get; }

    /// <param name="extension">«.gp» per le tablature, «.accordi» per i fogli di accordi: le copie non si mescolano.</param>
    public AutosaveStore(string? directory = null, string extension = ".gp")
        : this(Path.Combine(directory ?? DefaultDirectory, $"{Guid.NewGuid():N}{extension}"), createDirectory: true)
    {
    }

    private AutosaveStore(string path, bool createDirectory)
    {
        FilePath = path;
        if (createDirectory) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    /// <summary>Acquisisce una copia alla volta, dalla più recente. Il chiamante deve disporla.</summary>
    public static IEnumerable<AutosaveStore> FindRecoverable(string? directory = null, string extension = ".gp")
    {
        directory ??= DefaultDirectory;
        if (!Directory.Exists(directory)) yield break;
        foreach (var path in Directory.GetFiles(directory, "*" + extension)
                     .Where(p => string.Equals(Path.GetExtension(p), extension, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(File.GetLastWriteTimeUtc))
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
        Write(editor.ExportGp());
        return true;
    }

    /// <summary>Foglio di accordi: stessa scrittura atomica, solo se ci sono modifiche non salvate.</summary>
    public bool SaveIfNeeded(ChordSheetEditor editor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!editor.IsDirty) return false;
        Write(ChordSheetFile.ToBytes(editor.Sheet));
        return true;
    }

    // Una scrittura fallita lascia intatta la copia precedente.
    private void Write(byte[] bytes) => SafeFile.WriteAllBytes(FilePath, bytes);

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
