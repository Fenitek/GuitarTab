using System.Reflection;
using AlphaTab.Model;

namespace GuitarStudio.Editing;

/// <summary>
/// Copia profonda di tempi e note (alphaTab non espone un Clone pubblico).
/// Copia tutte le proprietà semplici (numeri, bool, enum, stringhe) e ricrea a mano le liste
/// che contano per la chitarra (punti di bend/leva, tremolo picking).
/// </summary>
public static class ModelCopy
{
    // Collegamenti e valori calcolati da Finish(): non vanno copiati.
    private static readonly HashSet<string> Skip = new()
    {
        "Id", "Index", "Voice", "Beat", "PreviousBeat", "NextBeat",
        "PlaybackStart", "PlaybackDuration", "DisplayStart", "DisplayDuration",
        "TieOrigin", "TieDestination", "SlideTarget", "SlideOrigin", "HammerPullOrigin", "HammerPullDestination",
        "SlurOrigin", "SlurDestination", "BendOrigin", "LetRingDestination", "PalmMuteDestination",
        "EffectSlurOrigin", "EffectSlurDestination", "GraceGroup", "Style", "MinNote", "MaxNote",
        "MinStringNote", "MaxStringNote", "Notes", "Automations", "Lyrics", "WhammyBarPoints",
        "BendPoints", "TremoloPicking"
    };

    private static readonly PropertyInfo[] BeatProps = SimpleProps(typeof(Beat));
    private static readonly PropertyInfo[] NoteProps = SimpleProps(typeof(Note));

    private static PropertyInfo[] SimpleProps(Type t) => t
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && !Skip.Contains(p.Name))
        .Where(p => IsSimple(p.PropertyType))
        .ToArray();

    private static bool IsSimple(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(double);
    }

    public static Beat CloneBeat(Beat source)
    {
        var beat = new Beat();
        foreach (var p in BeatProps)
        {
            try { p.SetValue(beat, p.GetValue(source)); }
            catch (TargetInvocationException) { /* proprietà calcolata: si ignora */ }
        }

        if (source.WhammyBarPoints != null)
            foreach (var bp in source.WhammyBarPoints)
                beat.AddWhammyBarPoint(new BendPoint(bp.Offset, bp.Value));

        if (source.TremoloPicking != null)
            beat.TremoloPicking = new TremoloPickingEffect { Marks = source.TremoloPicking.Marks, Style = source.TremoloPicking.Style };

        foreach (var n in source.Notes)
            beat.AddNote(CloneNote(n));
        return beat;
    }

    public static Note CloneNote(Note source)
    {
        var note = new Note();
        foreach (var p in NoteProps)
        {
            try { p.SetValue(note, p.GetValue(source)); }
            catch (TargetInvocationException) { }
        }
        if (source.BendPoints != null)
            foreach (var bp in source.BendPoints)
                note.AddBendPoint(new BendPoint(bp.Offset, bp.Value));
        return note;
    }
}
