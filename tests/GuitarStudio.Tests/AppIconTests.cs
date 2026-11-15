using System.Runtime.InteropServices;

namespace GuitarStudio.Tests;

public sealed class AppIconTests
{
    private static string AppDirectory => Path.GetDirectoryName(typeof(GuitarStudio.App).Assembly.Location)!;

    [Fact]
    public void IconFileContainsSmallAndLargeSizes()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppDirectory, "Assets", "GuitarStudio.ico"));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = Enumerable.Range(0, count).Select(i => bytes[6 + 16 * i] == 0 ? 256 : bytes[6 + 16 * i]).ToHashSet();
        Assert.Superset(new HashSet<int> { 16, 24, 32, 48, 256 }, sizes);
    }

    [Fact]
    public void ExecutableEmbedsTheIcon()
    {
        var exe = Path.Combine(AppDirectory, "GuitarStudio.exe");
        var module = File.Exists(exe) ? exe : typeof(GuitarStudio.App).Assembly.Location;
        Assert.True(ExtractIconEx(module, -1, null, null, 0) >= 1, $"Nessuna icona in {Path.GetFileName(module)}");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);
}
