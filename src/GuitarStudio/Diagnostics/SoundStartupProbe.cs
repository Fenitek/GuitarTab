using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GuitarStudio.Diagnostics;

/// <summary>
/// Misura memoria del processo e tempi del caricamento dei suoni nei due sintetizzatori
/// (lettore del brano e anteprime). Attiva solo con GS_PERF_SOUND_REPORT: senza la variabile
/// ogni chiamata ritorna subito e non alloca niente.
/// </summary>
internal static class SoundStartupProbe
{
    internal sealed record Sample(string Point, double SinceProcessStartMs, double SinceLoadStartMs,
        double PrivateMb, double WorkingSetMb, double ManagedMb);

    private static readonly string? ReportPath = Environment.GetEnvironmentVariable("GS_PERF_SOUND_REPORT");
    private static readonly List<Sample> Samples = new();
    private static long _loadStart;
    private static bool _playerReady, _previewReady, _written;

    internal static bool Enabled => ReportPath is { Length: > 0 };

    internal static void LoadStarted()
    {
        if (!Enabled || _loadStart != 0) return;
        Mark("prima del caricamento");
        _loadStart = Stopwatch.GetTimestamp();
    }

    internal static void PlayerReady()
    {
        if (!Enabled || _playerReady || _loadStart == 0) return;
        _playerReady = true;
        Mark("lettore del brano pronto");
        TryComplete();
    }

    internal static void PreviewReady()
    {
        if (!Enabled || _previewReady || _loadStart == 0) return;
        _previewReady = true;
        Mark("anteprime pronte");
        TryComplete();
    }

    private static void TryComplete()
    {
        if (!_playerReady || !_previewReady || _written) return;
        _written = true;
        Mark("suoni pronti");
        // Memoria trattenuta: senza garbage collection il dato include i buffer temporanei di lettura.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Mark("suoni pronti dopo GC");
        File.WriteAllText(ReportPath!, JsonSerializer.Serialize(Samples, new JsonSerializerOptions { WriteIndented = true }));
        // GS_PERF_SOUND_EXIT=1 chiude l'istanza di misura senza toccare mouse o tastiera.
        if (Environment.GetEnvironmentVariable("GS_PERF_SOUND_EXIT") == "1")
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
    }

    private static void Mark(string point)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var sinceStart = (DateTime.Now - process.StartTime).TotalMilliseconds;
        var sinceLoad = _loadStart == 0 ? 0 : Stopwatch.GetElapsedTime(_loadStart).TotalMilliseconds;
        const double mb = 1024 * 1024;
        Samples.Add(new Sample(point, Math.Round(sinceStart, 1), Math.Round(sinceLoad, 1),
            Math.Round(process.PrivateMemorySize64 / mb, 1), Math.Round(process.WorkingSet64 / mb, 1),
            Math.Round(GC.GetTotalMemory(false) / mb, 1)));
    }
}
