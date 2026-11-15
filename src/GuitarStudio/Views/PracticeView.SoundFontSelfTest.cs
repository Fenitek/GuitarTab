using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Midi;
using AlphaTab.Synth;

namespace GuitarStudio.Views;

public partial class PracticeView
{
    private async Task VerifySoundFont(LiveLog log)
    {
        var bytes = await File.ReadAllBytesAsync(BundledSoundFont);
        var results = await Task.Run(() =>
        {
            var samples = new List<(int Program, double Peak, string Hash)>();
            for (var program = 24; program <= 36; program++)
            {
                // Un motore nuovo per ogni programma: nessuna coda precedente può
                // falsare il confronto rendendo diversi due preset identici.
                var exporter = new AlphaSynthAudioExporter(new AudioExportOptions { SampleRate = 48000, MetronomeVolume = 0 });
                var midi = new MidiFile();
                midi.AddEvent(new ControlChangeEvent(0, 0, 0, ControllerType.AllSoundOff, 0));
                midi.AddEvent(new ProgramChangeEvent(0, 0, 0, program));
                midi.AddEvent(new NoteOnEvent(0, 0, 0, 60, 96));
                midi.AddEvent(new NoteOffEvent(0, 480, 0, 60, 0));
                midi.AddEvent(new EndOfTrackEvent(0, 481));
                exporter.LoadMidiFile(midi); exporter.LoadSoundFont(new Uint8Array(bytes)); exporter.Setup();
                var pcm = exporter.Render(150)!.Samples.ToArray();
                if (pcm.Any(sample => !float.IsFinite(sample))) throw new InvalidOperationException("Campioni audio non finiti.");
                var peak = pcm.Max(sample => Math.Abs((double)sample));
                var hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pcm.AsSpan())));
                samples.Add((program, peak, hash));
            }
            return samples;
        });
        foreach (var sample in results)
        {
            if (sample.Peak <= 0.0001) throw new InvalidOperationException($"Programma {sample.Program} silenzioso.");
            log.AppendLine($"SoundFont programma {sample.Program}: picco={sample.Peak:0.000000}, SHA256 PCM={sample.Hash}");
        }
        if (results.Select(r => r.Hash).Distinct().Count() != 13)
            throw new InvalidOperationException("Due programmi 24–36 producono campioni identici.");
        log.AppendLine("SoundFont OK: 13 programmi 24–36 non silenziosi e distinti (stessa nota/velocità/durata)");
        var report = Environment.GetEnvironmentVariable("GS_SELFTEST_SOUNDFONT_REPORT");
        if (!string.IsNullOrWhiteSpace(report)) File.WriteAllText(report, JsonSerializer.Serialize(results.Select(r => new
            { r.Program, r.Peak, r.Hash }), new JsonSerializerOptions { WriteIndented = true }));

        async Task Ready()
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while ((!_soundFontReady) && DateTime.UtcNow < deadline) await Task.Delay(20);
            if (!_soundFontReady) throw new InvalidOperationException("Caricamento SoundFont non terminato.");
        }
        var custom = Path.Combine(AppPaths.DataDirectory, "SoundFont-personale.sf2");
        Directory.CreateDirectory(AppPaths.DataDirectory); File.Copy(BundledSoundFont, custom, true);
        try
        {
            LoadSoundFont(custom, false); await Ready();
            if (AppSettings.Current.SoundFontPath != custom || !DefaultSoundFontItem.IsEnabled)
                throw new InvalidOperationException("SoundFont personale non conservato.");
            DefaultSoundFont_Click(this, new RoutedEventArgs()); await Ready();
            if (AppSettings.Current.SoundFontPath != null || DefaultSoundFontItem.IsEnabled)
                throw new InvalidOperationException("Ripristino SoundFont incluso non riuscito.");
            LoadSoundFont(custom + ".inesistente", false); await Ready();
            if (AppSettings.Current.SoundFontPath != null)
                throw new InvalidOperationException("Ripiego da SoundFont mancante non riuscito.");
            log.AppendLine("SoundFont OK: scelta personale, preferenza persistente, ripristino incluso e file mancante");
        }
        finally { File.Delete(custom); }
    }
}
