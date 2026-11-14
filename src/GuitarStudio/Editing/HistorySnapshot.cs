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

    /// <summary>Ripristina uno splice di battute, conservando soltanto la regione e i suoi collegamenti.</summary>
    internal sealed class BarSplice : HistorySnapshot
    {
        private readonly int _index;
        private readonly int _removeCount;
        private readonly object?[] _masters;
        private readonly Bars _context;

        internal BarSplice(Score score, int index, int oldCount, int newCount)
        {
            _index = index;
            _removeCount = newCount;
            _masters = score.MasterBars.Skip(index).Take(oldCount).Select(ModelData<MasterBar>.Write).ToArray();
            var first = Math.Max(0, index - (oldCount == 0 ? 1 : 0));
            var last = Math.Min(score.MasterBars.Count - 1, index + Math.Max(0, oldCount - 1));
            _context = Bars.Capture(score.Tracks.SelectMany(t => t.Staves)
                .SelectMany(s => s.Bars.Skip(first).Take(last - first + 1)));
        }

        internal override HistorySnapshot CaptureCurrent(TabEditor editor) =>
            new BarSplice(editor.Score!, _index, _removeCount, _masters.Length);

        internal override void Restore(TabEditor editor)
        {
            var score = editor.Score!;
            Splice(score.MasterBars, _index, _removeCount, _masters.Select(ModelData<MasterBar>.Read));
            foreach (var staff in score.Tracks.SelectMany(t => t.Staves))
            {
                Splice(staff.Bars, _index, _removeCount, _masters.Select(_ => new Bar()));
            }
            // La regione contiene anche tutte le Bar delle battute rimosse, in ogni staff.
            // Prima si ripristina la topologia; solo dopo si risolvono gli indirizzi delle note.
            _context.Restore(editor);
        }

        private static void Splice<T>(IList<T> list, int index, int removeCount, IEnumerable<T> insert)
        {
            for (var i = 0; i < removeCount; i++) list.RemoveAt(index);
            foreach (var item in insert) list.Insert(index++, item);
        }
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
            _bars = list.ToDictionary(AddressOf, ModelData<Bar>.Write);
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
                staff.Bars[address.Bar] = ModelData<Bar>.Read(data);
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
    private static class ModelData<T> where T : class, new()
    {
        private static readonly Assembly ModelAssembly = typeof(Score).Assembly;
        private static readonly Type WriterType = ModelAssembly.GetType("AlphaTab.Io.MapObjectWriter", throwOnError: true)!;
        private static readonly Type ReaderType = ModelAssembly.GetType("AlphaTab.Io.MapObjectReader", throwOnError: true)!;
        private static readonly Type Serializer = ModelAssembly.GetType($"AlphaTab.Generated.Model.{typeof(T).Name}Serializer", throwOnError: true)!;
        private static readonly MethodInfo WriteMethod = Serializer.GetMethod("Write")!;
        private static readonly MethodInfo ReadMethod = Serializer.GetMethod("Read")!;
        private static readonly PropertyInfo Result = WriterType.GetProperty("Result")!;

        internal static object? Write(T model)
        {
            var writer = Activator.CreateInstance(WriterType)!;
            WriteMethod.Invoke(null, [model, writer]);
            return Result.GetValue(writer);
        }

        internal static T Read(object? data)
        {
            var model = new T();
            var reader = Activator.CreateInstance(ReaderType, [data])!;
            ReadMethod.Invoke(null, [model, reader]);
            return model;
        }
    }
}
