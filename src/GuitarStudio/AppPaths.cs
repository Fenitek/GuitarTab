using System.IO;

namespace GuitarStudio;

internal static class AppPaths
{
    // I test visivi e le misure non devono consumare copie di recupero o riscrivere preferenze personali.
    internal static string DataDirectory => IsolatedRun &&
        Environment.GetEnvironmentVariable("GS_SELFTEST_DATA_DIR") is { Length: > 0 } testDirectory
            ? Path.GetFullPath(testDirectory)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuitarStudio");

    private static bool IsolatedRun => Environment.GetEnvironmentVariable("GS_SELFTEST") == "1" ||
                                       Environment.GetEnvironmentVariable("GS_PERF_SOUND_REPORT") is { Length: > 0 };
}
