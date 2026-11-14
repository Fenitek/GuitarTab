using System.IO;

namespace GuitarStudio;

internal static class AppPaths
{
    // I test visivi non devono consumare copie di recupero o riscrivere preferenze personali.
    internal static string DataDirectory => Environment.GetEnvironmentVariable("GS_SELFTEST") == "1" &&
        Environment.GetEnvironmentVariable("GS_SELFTEST_DATA_DIR") is { Length: > 0 } testDirectory
            ? Path.GetFullPath(testDirectory)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuitarStudio");
}
