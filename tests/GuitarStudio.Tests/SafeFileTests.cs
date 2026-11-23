using System.Text;
using GuitarStudio.Recovery;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class SafeFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GuitarStudio.Tests", Guid.NewGuid().ToString("N"));

    public SafeFileTests() => Directory.CreateDirectory(_directory);

    private string[] Leftovers() => Directory.GetFiles(_directory, "*.tmp", SearchOption.TopDirectoryOnly);

    [Fact]
    public void CreatesAndReplacesGpAndAtexWithoutTemporaryFiles()
    {
        var e = Create();
        e.SetFret(5);
        var gp = Path.Combine(_directory, "brano.gp");
        SafeFile.WriteAllBytes(gp, e.ExportGp());
        Assert.Equal(Content(e.Score!), Content(Read(File.ReadAllBytes(gp))));
        e.SetFret(7);
        SafeFile.WriteAllBytes(gp, e.ExportGp());
        Assert.Equal(Content(e.Score!), Content(Read(File.ReadAllBytes(gp))));

        var atex = Path.Combine(_directory, "brano.atex");
        File.WriteAllText(atex, "vecchio");
        var tex = e.ExportAlphaTex();
        SafeFile.WriteAllText(atex, tex);
        var bytes = File.ReadAllBytes(atex);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..]); // come File.WriteAllText: niente BOM
        Assert.Equal(tex, Encoding.UTF8.GetString(bytes));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public void FailureWhileWritingKeepsOriginalAndRemovesTemporary()
    {
        var path = Path.Combine(_directory, "brano.gp");
        File.WriteAllBytes(path, [1, 2, 3]);
        var error = Record.Exception(() => SafeFile.Write(path, stream =>
        {
            stream.Write(new byte[4096]);
            throw new IOException("disco pieno (simulato)");
        }));
        Assert.IsType<IOException>(error);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public void FailureWhileReplacingKeepsOriginalAndRemovesTemporary()
    {
        var path = Path.Combine(_directory, "brano.atex");
        File.WriteAllText(path, "originale");
        // Un altro programma tiene aperto il file: la sostituzione non può avvenire.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => SafeFile.WriteAllText(path, "nuovo"));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("originale", File.ReadAllText(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public void ReadOnlyDestinationIsNotOverwritten()
    {
        var path = Path.Combine(_directory, "protetto.gp");
        File.WriteAllBytes(path, [9]);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var error = Record.Exception(() => SafeFile.WriteAllBytes(path, [1, 2]));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.Equal([9], File.ReadAllBytes(path));
            Assert.Empty(Leftovers());
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }

    [Fact]
    public void MissingFolderFailsWithoutCreatingAnything()
    {
        var path = Path.Combine(_directory, "manca", "brano.gp");
        Assert.ThrowsAny<IOException>(() => SafeFile.WriteAllBytes(path, [1]));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void ChordSheetWriteLeavesUserTmpFileUntouched()
    {
        var path = Path.Combine(_directory, "Perché.accordi");
        var sentinel = path + ".tmp";
        byte[] sentinelBytes = [0xC3, 0xA8, 1, 2, 3];
        File.WriteAllBytes(sentinel, sentinelBytes);
        var sheet = GuitarStudio.Editing.ChordSheet.New() with { Title = "Perché è così" };
        GuitarStudio.Editing.ChordSheetFile.Write(path, sheet);
        Assert.Equal(GuitarStudio.Editing.ChordSheetFile.ToBytes(sheet), File.ReadAllBytes(path));
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => GuitarStudio.Editing.ChordSheetFile.Write(path, sheet with { Title = "Altro" }));
            Assert.True(error is IOException or UnauthorizedAccessException); // quelle che la vista gestisce
        }
        Assert.Equal("Perché è così", GuitarStudio.Editing.ChordSheetFile.Read(path).Title);
        Assert.Equal(sentinelBytes, File.ReadAllBytes(sentinel));
        Assert.Equal([sentinel], Leftovers());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
