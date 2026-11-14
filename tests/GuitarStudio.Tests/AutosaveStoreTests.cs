using GuitarStudio.Editing;
using GuitarStudio.Recovery;
using static GuitarStudio.Tests.EditorFixture;

namespace GuitarStudio.Tests;

public sealed class AutosaveStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GuitarStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CleanDocumentDoesNotCreateGpAndDirtyCopyRoundTrips()
    {
        using var store = new AutosaveStore(_directory);
        var e = Create();
        Assert.False(store.SaveIfNeeded(e));
        Assert.False(File.Exists(store.FilePath));
        e.SetFret(12);
        var content = Content(e.Score!);
        Assert.True(store.SaveIfNeeded(e));
        Assert.True(e.IsDirty);
        Assert.Equal(content, Content(Read(File.ReadAllBytes(store.FilePath))));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void ActiveSessionsCannotBeRecoveredAndCopiesAreIndependent()
    {
        using var first = new AutosaveStore(_directory);
        using var second = new AutosaveStore(_directory);
        var e = Create();
        e.SetFret(12);
        first.SaveIfNeeded(e);
        second.SaveIfNeeded(e);
        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Empty(AutosaveStore.FindRecoverable(_directory));
        first.Delete();
        Assert.False(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
    }

    [Fact]
    public void InterruptedSessionCanBeRecoveredAndRemainsDirtyUntilExplicitSave()
    {
        var e = Create();
        e.SetFret(24);
        string path;
        using (var interrupted = new AutosaveStore(_directory))
        {
            interrupted.SaveIfNeeded(e);
            path = interrupted.FilePath;
        }
        // Simulate a stale lease left on disk by a process that died.
        File.WriteAllText(path + ".lock", "");
        using var recovery = Assert.Single(AutosaveStore.FindRecoverable(_directory));
        Assert.Equal(path, recovery.FilePath);
        var recoveredEditor = new TabEditor(new AlphaTab.Settings());
        recoveredEditor.Attach(Read(File.ReadAllBytes(path)), 0, isDirty: true);
        Assert.True(recoveredEditor.IsDirty);
        Assert.False(recoveredEditor.CanUndo);
        Assert.Equal(Content(e.Score!), Content(recoveredEditor.Score!));
        Assert.Empty(AutosaveStore.FindRecoverable(_directory));
        recoveredEditor.MarkSaved();
        recovery.Delete();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void FailedWritePreservesPreviousCopyAndDirtyState()
    {
        using var store = new AutosaveStore(_directory);
        var e = Create();
        e.SetFret(12);
        store.SaveIfNeeded(e);
        var original = File.ReadAllBytes(store.FilePath);
        e.SetFret(24);
        using (var blocked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => store.SaveIfNeeded(e));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(original, File.ReadAllBytes(store.FilePath));
        Assert.True(e.IsDirty);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.True(store.SaveIfNeeded(e));
        Assert.Equal(Content(e.Score!), Content(Read(File.ReadAllBytes(store.FilePath))));
    }

    [Fact]
    public void PostponedRecoveryRemainsAvailableAndCleanCloseDeletesOnlyOwnedCopy()
    {
        var e = Create();
        e.SetFret(12);
        string oldPath;
        using (var interrupted = new AutosaveStore(_directory))
        {
            interrupted.SaveIfNeeded(e);
            oldPath = interrupted.FilePath;
        }
        using (var postponed = Assert.Single(AutosaveStore.FindRecoverable(_directory))) { }
        using (var current = new AutosaveStore(_directory))
        {
            current.SaveIfNeeded(e);
            current.Delete();
        }
        Assert.True(File.Exists(oldPath));
        using var discarded = Assert.Single(AutosaveStore.FindRecoverable(_directory));
        discarded.Delete();
        Assert.Empty(AutosaveStore.FindRecoverable(_directory));
    }

    public void Dispose()
    {
        // Every test owns a random directory created beneath TEMP.
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
