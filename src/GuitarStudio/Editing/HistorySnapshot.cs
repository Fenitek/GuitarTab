using System.Reflection;
using AlphaTab.Model;
using AlphaTab.Collections;

namespace GuitarStudio.Editing;

internal abstract class HistorySnapshot
{
    internal virtual IReadOnlyList<int>? BarIndices => null;
    internal abstract HistorySnapshot CaptureCurrent(TabEditor editor);
    internal abstract void Restore(TabEditor editor);

    internal sealed class Full(byte[] bytes) : HistorySnapshot
    {
        internal override HistorySnapshot CaptureCurrent(TabEditor editor) => new Full(editor.ExportGp());
        internal override void Restore(TabEditor editor) => editor.RestoreScore(bytes);
    }

    internal sealed class Bars : HistorySnapshot
    {
        internal override IReadOnlyList<int> BarIndices => _bars.Keys.Select(a => a.Bar).Distinct().Order().ToArray();
        private readonly record struct Address(int Track, int Staff, int Bar);
        private readonly Dictionary<Address, object?> _bars;
        private readonly record struct NoteAddress(Address Bar, int Voice, int Beat, int Note);
        private readonly Dictionary<double, NoteAddress> _noteAddresses = new();

        internal Bars(IEnumerable<Bar> bars)
        {
            var list = bars.Distinct().ToList();
            _bars = list.ToDictionary(AddressOf, BarData.Write);
            foreach (var note in list.SelectMany(b => b.Voices).SelectMany(v => v.Beats).SelectMany(b => b.Notes))
            {
                Remember(note);
                foreach (var link in NoteLinks)
                    if (link.GetValue(note) is Note target) Remember(target);
            }
        }

        private static Address AddressOf(Bar b) => new((int)b.Staff.Track.Index, (int)b.Staff.Index, (int)b.Index);
        private void Remember(Note note) => _noteAddresses[note.Id] = new NoteAddress(
            AddressOf(note.Beat.Voice.Bar), (int)note.Beat.Voice.Index, (int)note.Beat.Index, (int)note.Index);

        internal override HistorySnapshot CaptureCurrent(TabEditor editor) => new Bars(_bars.Keys.Select(a =>
            editor.Score!.Tracks[a.Track].Staves[a.Staff].Bars[a.Bar]));

        internal override void Restore(TabEditor editor)
        {
            foreach (var (address, data) in _bars)
            {
                var staff = editor.Score!.Tracks[address.Track].Staves[address.Staff];
                staff.Bars[address.Bar] = BarData.Read(data);
            }
            editor.FinishRestoredBars(() =>
            {
                var lookup = new Map<double, Note>();
                foreach (var (id, a) in _noteAddresses)
                    lookup.Set(id, editor.Score!.Tracks[a.Bar.Track].Staves[a.Bar.Staff].Bars[a.Bar.Bar]
                        .Voices[a.Voice].Beats[a.Beat].Notes[a.Note]);
                var data = new Map<string, object?>();
                data.Set("NoteIdLookup", lookup);
                foreach (var a in _bars.Keys)
                    foreach (var beat in editor.Score!.Tracks[a.Track].Staves[a.Staff].Bars[a.Bar].Voices.SelectMany(v => v.Beats))
                        beat.Chain(data);
            });
        }

        // Finish può rimuovere un hammer/slide/legatura nella battuta collegata a una nota
        // cancellata. Conserva anche i suoi estremi, senza clonare le altre 199 battute.
        private static readonly PropertyInfo[] NoteLinks = typeof(Note).GetProperties()
            .Where(p => p.CanRead && (p.PropertyType == typeof(Note) || p.PropertyType == typeof(Beat))).ToArray();
        private static readonly PropertyInfo[] BeatLinks = typeof(Beat).GetProperties()
            .Where(p => p.CanRead && p.PropertyType == typeof(Beat)).ToArray();

        internal static Bars Capture(IEnumerable<Bar> changed)
        {
            var bars = changed.Distinct().ToHashSet();
            foreach (var bar in bars.ToArray())
                foreach (var beat in bar.Voices.SelectMany(v => v.Beats))
                {
                    foreach (var link in BeatLinks)
                        if (link.GetValue(beat) is Beat target) bars.Add(target.Voice.Bar);
                    foreach (var note in beat.Notes)
                        foreach (var link in NoteLinks)
                        {
                            var target = link.GetValue(note) switch { Note n => n.Beat, Beat b => b, _ => null };
                            if (target?.Voice?.Bar is { } linked) bars.Add(linked);
                        }
                }
            return new Bars(bars);
        }
    }

    // alphaTab 1.9 non espone un clone lossless della battuta. Il suo serializzatore include
    // automazioni, testi e stili che ModelCopy (destinato agli appunti) non copia.
    // Read non esegue Finish su una battuta isolata: le legature tra battute restano intatte.
    // Questo adattatore è l'unico punto dipendente dai tipi interni della versione NuGet fissata.
    private static class BarData
    {
        private static readonly Assembly ModelAssembly = typeof(Score).Assembly;
        private static readonly Type WriterType = ModelAssembly.GetType("AlphaTab.Io.MapObjectWriter", throwOnError: true)!;
        private static readonly Type ReaderType = ModelAssembly.GetType("AlphaTab.Io.MapObjectReader", throwOnError: true)!;
        private static readonly Type Serializer = ModelAssembly.GetType("AlphaTab.Generated.Model.BarSerializer", throwOnError: true)!;
        private static readonly MethodInfo WriteMethod = Serializer.GetMethod("Write")!;
        private static readonly MethodInfo ReadMethod = Serializer.GetMethod("Read")!;
        private static readonly PropertyInfo Result = WriterType.GetProperty("Result")!;

        internal static object? Write(Bar bar)
        {
            var writer = Activator.CreateInstance(WriterType)!;
            WriteMethod.Invoke(null, [bar, writer]);
            return Result.GetValue(writer);
        }

        internal static Bar Read(object? data)
        {
            var bar = new Bar();
            var reader = Activator.CreateInstance(ReaderType, [data])!;
            ReadMethod.Invoke(null, [bar, reader]);
            return bar;
        }
    }
}
