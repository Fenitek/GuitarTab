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
                // Solo l'autotest arma il guasto, e vale per una sola scrittura.
                var fault = Interlocked.Exchange(ref _selfTestFault, null);
                write(fault == null ? stream : new FaultyStream(stream, fault));
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

    private static SelfTestFault? _selfTestFault;

    /// <summary>
    /// Solo per l'autotest: la prossima scrittura si interrompe con un errore dopo
    /// <paramref name="afterBytes"/> byte già finiti nel file temporaneo. Il risultato dice quanti.
    /// </summary>
    internal static SelfTestFault ArmSelfTestFault(int afterBytes) =>
        _selfTestFault = new SelfTestFault(afterBytes);

    internal sealed class SelfTestFault(int afterBytes)
    {
        internal int AfterBytes { get; } = afterBytes;
        internal string? TemporaryPath { get; set; }
        internal long BytesOnDisk { get; set; } = -1;
        internal bool Triggered => BytesOnDisk >= 0;
    }

    /// <summary>Lascia passare i primi byte, li manda su disco e poi fallisce come un disco pieno.</summary>
    private sealed class FaultyStream(FileStream inner, SelfTestFault fault) : Stream
    {
        private long _written;

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var allowed = (int)Math.Max(0, Math.Min(buffer.Length, fault.AfterBytes - _written));
            inner.Write(buffer[..allowed]);
            _written += allowed;
            if (allowed == buffer.Length) return;
            inner.Flush(flushToDisk: true);
            fault.TemporaryPath = inner.Name;
            fault.BytesOnDisk = inner.Length;
            throw new IOException("Errore di scrittura simulato dall'autotest.");
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
