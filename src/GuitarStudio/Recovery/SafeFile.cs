using System.IO;
using System.Text;

namespace GuitarStudio.Recovery;

/// <summary>
/// Scrittura che non lascia mai un file a metà: il contenuto va in un file temporaneo univoco
/// nella stessa cartella e sostituisce la destinazione solo quando è completo e su disco.
/// Se qualcosa va storto la destinazione resta quella di prima e il temporaneo si elimina.
/// </summary>
public static class SafeFile
{
    public static void WriteAllBytes(string path, byte[] bytes) => Write(path, stream => stream.Write(bytes));

    /// <summary>UTF-8 senza BOM, come File.WriteAllText.</summary>
    public static void WriteAllText(string path, string text) =>
        WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));

    /// <param name="write">Scrive il contenuto nel flusso del file temporaneo; un'eccezione annulla tutto.</param>
    public static void Write(string path, Action<Stream> write)
    {
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)
                        ?? throw new IOException($"Percorso non valido: {path}");
        // Stessa cartella, quindi stesso volume: la sostituzione finale è un solo passo del file system.
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { /* Resta solo un .tmp nascosto: l'errore originale conta di più. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
