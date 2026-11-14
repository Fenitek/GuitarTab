using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Exporter;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Model;

namespace GuitarStudio.Editing;

public enum NoteEffect
{
    // Effetti sulla nota
    HammerPull,
    Slide,
    SlideShift,
    SlideInBelow,
    SlideOutDown,
    Bend,
    BendHalf,
    BendRelease,
    PrebendRelease,
    Vibrato,
    VibratoWide,
    PalmMute,
    Dead,
    Ghost,
    LetRing,
    Tie,
    Staccato,
    Accent,
    HeavyAccent,
    HarmonicNatural,
    HarmonicArtificial,
    HarmonicPinch,
    LeftHandTap,

    // Effetti sul tempo (valgono per tutte le note del tempo)
    Tap,
    PickUp,
    PickDown,
    Tremolo
}

/// <summary>Proprietà di una battuta modificabili dall'utente.</summary>
public sealed record BarInfo(
    double? TempoChange,
    double CurrentTempo,
    int Numerator,
    int Denominator,
    bool RepeatStart,
    int RepeatCount,
    string? Section);

/// <summary>
/// Logica dell'editor di tablature: cursore, selezione, appunti e modifiche al modello di alphaTab.
/// Non sa nulla dell'interfaccia: chi lo usa ascolta <see cref="Changed"/> e <see cref="CursorMoved"/>.
/// </summary>
public sealed class TabEditor
{
    public static readonly Duration[] Durations =
    {
        Duration.Whole, Duration.Half, Duration.Quarter, Duration.Eighth,
        Duration.Sixteenth, Duration.ThirtySecond, Duration.SixtyFourth
    };

    private static readonly HashSet<NoteEffect> BeatEffects =
        new() { NoteEffect.Tap, NoteEffect.PickUp, NoteEffect.PickDown, NoteEffect.Tremolo };

    private const int MaxUndo = 100;
    private const int MaxFret = 30;
    private const int DrumChannel = 9;

    private readonly Settings _settings;
    private readonly List<HistorySnapshot> _undo = new();
    private readonly List<HistorySnapshot> _redo = new();

    // Selezione: punto d'ancoraggio (battuta, tempo); l'altro estremo è il cursore.
    private (int Bar, int Beat)? _anchor;

    // Appunti: battute (liste di tempi clonati) e se sono battute intere.
    private List<List<Beat>>? _clipboard;
    private bool _clipboardWholeBars;

    public TabEditor(Settings settings)
    {
        _settings = settings;
    }

    public Score? Score { get; private set; }
    public int TrackIndex { get; private set; }
    public int BarIndex { get; private set; }
    public int BeatIndex { get; private set; }
    public int VoiceIndex { get; private set; }

    /// <summary>Corda selezionata come la vede il chitarrista: 1 = la più acuta (in alto nella tab).</summary>
    public int Line { get; private set; } = 1;

    public Duration CurrentDuration { get; private set; } = Duration.Quarter;
    public bool IsDirty { get; private set; }
    /// <summary>Battute interessate dall'ultimo edit locale; null per un cambiamento globale.</summary>
    public IReadOnlyList<int>? ChangedBars { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool HasSelection => _anchor != null && _anchor != (BarIndex, BeatIndex);
    public bool HasClipboard => _clipboard is { Count: > 0 };

    /// <summary>Il modello è cambiato: va ridisegnato.</summary>
    public event Action? Changed;

    /// <summary>Si è spostato solo il cursore (o la selezione).</summary>
    public event Action? CursorMoved;

    /// <summary>Sono cambiate le tracce (aggiunte, tolte, rinominate).</summary>
    public event Action? TracksChanged;

    // ---------------------------------------------------------------- accesso al modello

    public Track? Track => Score != null && TrackIndex < Score.Tracks.Count ? Score.Tracks[TrackIndex] : null;
    public Staff? Staff => Track?.Staves.Count > 0 ? Track.Staves[0] : null;
    public int StringCount => Staff?.Tuning.Count ?? 0;
    public bool IsEditable => Staff is { IsStringed: true } && !Track!.IsPercussion && StringCount > 0;
    public Bar? CurrentBar => Staff != null && BarIndex < Staff.Bars.Count ? Staff.Bars[BarIndex] : null;
    public Voice? CurrentVoice => CurrentBar is { } bar && bar.Voices.Count > VoiceIndex ? bar.Voices[VoiceIndex] : null;
    public Beat? CurrentBeat => CurrentVoice is { } v && BeatIndex < v.Beats.Count ? v.Beats[BeatIndex] : null;
    public Note? CurrentNote => CurrentBeat?.GetNoteOnString(ModelString(Line));

    /// <summary>In alphaTab la corda 1 è la più grave; per l'utente la 1 è la più acuta.</summary>
    public int ModelString(int line) => StringCount - line + 1;
    public int LineOf(int modelString) => StringCount - modelString + 1;

    /// <summary>Nota MIDI della corda a vuoto (capotasto incluso).</summary>
    public int OpenStringPitch(int line) =>
        Staff == null ? 0 : (int)Note.GetStringTuning(Staff, ModelString(line)) + (int)Staff.Capo;

    // ---------------------------------------------------------------- caricamento

    public void Attach(Score score, int trackIndex, bool isDirty = false)
    {
        Score = score;
        TrackIndex = Math.Clamp(trackIndex, 0, Math.Max(0, score.Tracks.Count - 1));
        BarIndex = 0;
        BeatIndex = 0;
        VoiceIndex = 0;
        Line = 1;
        _anchor = null;
        IsDirty = isDirty;
        ChangedBars = null;
        _undo.Clear();
        _redo.Clear();
        ClampCursor();
        CursorMoved?.Invoke();
    }

    public void SelectTrack(int trackIndex)
    {
        if (Score == null) return;
        TrackIndex = Math.Clamp(trackIndex, 0, Score.Tracks.Count - 1);
        VoiceIndex = 0;
        _anchor = null;
        ClampCursor();
        CursorMoved?.Invoke();
    }

    public void MarkSaved() => IsDirty = false;

    /// <summary>Seleziona una voce indipendente, mantenendo la posizione ritmica quando possibile.</summary>
    public void SelectVoice(int index)
    {
        if (!IsEditable) return;
        index = Math.Clamp(index, 0, 1);
        var tick = CurrentBeat?.PlaybackStart ?? 0;
        VoiceIndex = index;
        _anchor = null;
        ActivateVoice();
        var beats = CurrentVoice!.Beats;
        BeatIndex = Enumerable.Range(0, beats.Count).LastOrDefault(i => beats[i].PlaybackStart <= tick);
        ClampCursor();
        CursorMoved?.Invoke();
    }

    private void ActivateVoice()
    {
        if (Staff == null || (Staff.Bars.All(b => b.Voices.Count > VoiceIndex) &&
                             CurrentVoice is { } voice && voice.Beats.Any(b => !b.IsEmpty))) return;
        Edit(() =>
        {
            foreach (var bar in Staff.Bars)
            {
                while (bar.Voices.Count <= VoiceIndex)
                {
                    var added = new Voice();
                    bar.AddVoice(added);
                    foreach (var rest in RestsFor(bar.MasterBar, Duration.Quarter))
                    {
                        rest.IsEmpty = true;
                        added.AddBeat(rest);
                    }
                }
            }
            foreach (var beat in CurrentVoice!.Beats) beat.IsEmpty = false;
            Relink();
        });
    }

    public byte[] ExportGp() => ToBytes(new Gp7Exporter().Export(Score!, _settings));

    /// <summary>File MIDI standard con tutte le tracce.</summary>
    public byte[] ExportMidi()
    {
        var file = new MidiFile();
        new MidiFileGenerator(Score!, _settings, new AlphaSynthMidiFileHandler(file, true)).Generate();
        return ToBytes(file.ToBinary());
    }

    public string ExportAlphaTex() => new AlphaTexExporter().ExportToString(Score!, _settings);

    private Score Import(byte[] bytes) => ScoreLoader.LoadScoreFromBytes(new Uint8Array(bytes), _settings);

    private static byte[] ToBytes(Uint8Array a)
    {
        var raw = a.Buffer.Raw;
        var offset = (int)a.ByteOffset;
        var length = (int)a.Length;
        if (offset == 0 && length == raw.Length) return raw;
        return raw.AsSpan(offset, length).ToArray();
    }

    // ---------------------------------------------------------------- cursore

    public void MoveTo(int barIndex, int beatIndex, int line, bool extendSelection = false)
    {
        if (extendSelection) _anchor ??= (BarIndex, BeatIndex);
        else _anchor = null;
        BarIndex = Staff == null ? 0 : Math.Clamp(barIndex, 0, Staff.Bars.Count - 1);
        BeatIndex = beatIndex;
        Line = line;
        if (VoiceIndex > 0) ActivateVoice();
        ClampCursor();
        CursorMoved?.Invoke();
    }

    public void MoveUp() => MoveTo(BarIndex, BeatIndex, Line - 1, _anchor != null);
    public void MoveDown() => MoveTo(BarIndex, BeatIndex, Line + 1, _anchor != null);

    public void MoveLeft(bool select = false)
    {
        if (BeatIndex > 0) MoveTo(BarIndex, BeatIndex - 1, Line, select);
        else if (BarIndex > 0) MoveTo(BarIndex - 1, int.MaxValue, Line, select);
        else MoveTo(BarIndex, BeatIndex, Line, select);
    }

    /// <summary>
    /// Come in Guitar Pro: oltre l'ultimo tempo di una battuta incompleta aggiunge una pausa,
    /// oltre l'ultima battuta ne crea una nuova. Con la selezione si ferma alla fine.
    /// </summary>
    public void MoveRight(bool select = false)
    {
        if (!IsEditable || CurrentVoice is not { } voice) return;

        if (BeatIndex < voice.Beats.Count - 1)
        {
            MoveTo(BarIndex, BeatIndex + 1, Line, select);
            return;
        }
        if (BarIndex < Staff!.Bars.Count - 1)
        {
            if (!select && BarFill(out var u, out var t) && u < t - 0.5)
            {
                AppendRestInBar(voice);
                return;
            }
            MoveTo(BarIndex + 1, 0, Line, select);
            return;
        }
        if (select) return;

        if (BarFill(out var used, out var total) && used < total - 0.5)
        {
            AppendRestInBar(voice);
            return;
        }
        Edit(() =>
        {
            InsertBarAt(BarIndex + 1);
            BarIndex++;
            BeatIndex = 0;
        }, new HistorySnapshot.BarSplice(Score!, BarIndex + 1, 0, 1));
    }

    private void AppendRestInBar(Voice voice)
    {
        EditLocal(() =>
        {
            voice.AddBeat(NewRest(CurrentDuration));
            Relink();
            BeatIndex = voice.Beats.Count - 1;
        });
    }

    public void PreviousBar(bool select = false) => MoveTo(Math.Max(0, BarIndex - 1), 0, Line, select);
    public void NextBar(bool select = false) => MoveTo(BarIndex + 1, 0, Line, select);
    public void FirstBeat(bool select = false) => MoveTo(BarIndex, 0, Line, select);
    public void LastBeat(bool select = false) => MoveTo(BarIndex, int.MaxValue, Line, select);

    public void ClearSelection()
    {
        if (_anchor == null) return;
        _anchor = null;
        CursorMoved?.Invoke();
    }

    /// <summary>Seleziona tutta la traccia.</summary>
    public void SelectAll()
    {
        if (Staff == null) return;
        _anchor = (0, 0);
        BarIndex = Staff.Bars.Count - 1;
        BeatIndex = int.MaxValue;
        ClampCursor();
        CursorMoved?.Invoke();
    }

    private void ClampCursor()
    {
        if (Staff == null || Staff.Bars.Count == 0)
        {
            BarIndex = BeatIndex = 0;
            Line = 1;
            _anchor = null;
            return;
        }
        BarIndex = Math.Clamp(BarIndex, 0, Staff.Bars.Count - 1);
        VoiceIndex = Math.Clamp(VoiceIndex, 0, Math.Max(0, CurrentBar!.Voices.Count - 1));
        var beats = CurrentVoice?.Beats.Count ?? 1;
        BeatIndex = Math.Clamp(BeatIndex, 0, Math.Max(0, beats - 1));
        Line = Math.Clamp(Line, 1, Math.Max(1, StringCount));

        if (_anchor is { } a)
        {
            var bar = Math.Clamp(a.Bar, 0, Staff.Bars.Count - 1);
            var count = Staff.Bars[bar].Voices.Count > VoiceIndex ? Staff.Bars[bar].Voices[VoiceIndex].Beats.Count : 1;
            _anchor = (bar, Math.Clamp(a.Beat, 0, Math.Max(0, count - 1)));
        }
    }

    // ---------------------------------------------------------------- selezione

    /// <summary>Estremi della selezione in ordine, oppure il solo tempo corrente.</summary>
    public ((int Bar, int Beat) From, (int Bar, int Beat) To) SelectionRange()
    {
        var cur = (BarIndex, BeatIndex);
        if (_anchor is not { } a) return (cur, cur);
        return Compare(a, cur) <= 0 ? (a, cur) : (cur, a);
    }

    private static int Compare((int Bar, int Beat) x, (int Bar, int Beat) y) =>
        x.Bar != y.Bar ? x.Bar.CompareTo(y.Bar) : x.Beat.CompareTo(y.Beat);

    /// <summary>Tempi selezionati della voce corrente, o quello sotto il cursore.</summary>
    public List<Beat> SelectedBeats()
    {
        var result = new List<Beat>();
        if (Staff == null) return result;
        var (from, to) = SelectionRange();
        for (var b = from.Bar; b <= to.Bar && b < Staff.Bars.Count; b++)
        {
            var bar = Staff.Bars[b];
            if (bar.Voices.Count <= VoiceIndex) continue;
            var beats = bar.Voices[VoiceIndex].Beats;
            var start = b == from.Bar ? from.Beat : 0;
            var end = b == to.Bar ? to.Beat : beats.Count - 1;
            for (var i = start; i <= end && i < beats.Count; i++) result.Add(beats[i]);
        }
        return result;
    }

    /// <summary>True se la selezione copre battute intere (dal primo all'ultimo tempo).</summary>
    public bool SelectionIsWholeBars(out int fromBar, out int toBar)
    {
        var (from, to) = SelectionRange();
        fromBar = from.Bar;
        toBar = to.Bar;
        if (!HasSelection || Staff == null) return false;
        var lastBeat = Staff.Bars[to.Bar].Voices[VoiceIndex].Beats.Count - 1;
        return from.Beat == 0 && to.Beat == lastBeat;
    }

    // ---------------------------------------------------------------- appunti

    /// <summary>Copia la selezione; senza selezione copia la battuta corrente.</summary>
    public int Copy()
    {
        if (!IsEditable) return 0;
        List<List<Beat>> clip = new();
        if (!HasSelection || SelectionIsWholeBars(out _, out _))
        {
            int fromBar = BarIndex, toBar = BarIndex;
            if (HasSelection) (fromBar, toBar) = (SelectionRange().From.Bar, SelectionRange().To.Bar);
            var (from, to) = ((Bar: fromBar, Beat: 0), (Bar: toBar, Beat: 0));
            for (var b = from.Bar; b <= to.Bar; b++)
                clip.Add(Staff!.Bars[b].Voices[VoiceIndex].Beats.Select(ModelCopy.CloneBeat).ToList());
            _clipboardWholeBars = true;
        }
        else
        {
            clip.Add(SelectedBeats().Select(ModelCopy.CloneBeat).ToList());
            _clipboardWholeBars = false;
        }
        _clipboard = clip;
        return clip.Sum(c => c.Count);
    }

    public void Cut()
    {
        if (Copy() == 0) return;
        if (!HasSelection) { _anchor = (BarIndex, 0); BeatIndex = CurrentVoice!.Beats.Count - 1; }
        DeleteSelection();
    }

    /// <summary>
    /// Battute intere: vengono inserite dopo quella corrente (o la sostituiscono se è vuota).
    /// Tempi sparsi: vengono inseriti prima del cursore.
    /// </summary>
    public void Paste()
    {
        if (!IsEditable || _clipboard is not { Count: > 0 } clip || CurrentVoice is not { } voice) return;
        var replace = voice.Beats.All(b => b.Notes.Count == 0);
        var targetBar = replace ? BarIndex : BarIndex + 1;
        Edit(() =>
        {
            if (_clipboardWholeBars)
            {
                var target = voice.Beats.All(b => b.Notes.Count == 0) ? BarIndex : BarIndex + 1;
                for (var i = 0; i < clip.Count; i++)
                {
                    var index = target + i;
                    if (!(i == 0 && target == BarIndex)) InsertBarAt(index);
                    var v = Staff!.Bars[index].Voices[VoiceIndex];
                    v.Beats.Clear();
                    foreach (var beat in clip[i]) v.AddBeat(ModelCopy.CloneBeat(beat));
                }
                Relink();
                BarIndex = target + clip.Count - 1;
                BeatIndex = 0;
            }
            else
            {
                var insertAt = BeatIndex;
                foreach (var beat in clip[0])
                {
                    var copy = ModelCopy.CloneBeat(beat);
                    copy.Voice = voice;
                    voice.Beats.Insert(insertAt++, copy);
                }
                Relink();
                BeatIndex = Math.Min(insertAt, voice.Beats.Count - 1);
            }
            _anchor = null;
        }, _clipboardWholeBars
            ? new HistorySnapshot.BarSplice(Score!, targetBar, replace ? 1 : 0, clip.Count)
            : HistorySnapshot.Bars.Capture([CurrentBar!]));
    }

    /// <summary>Elimina i tempi della sola voce selezionata, preservando le altre voci.</summary>
    public void DeleteSelection()
    {
        if (!IsEditable) return;
        var beats = SelectedBeats();
        if (beats.Count == 0) return;
        var (from, _) = SelectionRange();
        EditSelected(() =>
        {
            foreach (var beat in beats)
            {
                var v = beat.Voice;
                v.Beats.Remove(beat);
                if (v.Beats.Count == 0)
                    foreach (var rest in RestsFor(v.Bar.MasterBar, Duration.Quarter)) v.AddBeat(rest);
            }
            Relink();
            BarIndex = from.Bar;
            BeatIndex = from.Beat;
            _anchor = null;
        });
    }

    // ---------------------------------------------------------------- note

    public void SetFret(int fret)
    {
        if (!IsEditable || CurrentBeat is not { } beat) return;
        fret = Math.Clamp(fret, 0, MaxFret);
        EditLocal(() =>
        {
            var str = ModelString(Line);
            var note = beat.GetNoteOnString(str);
            if (note == null)
            {
                note = new Note { String = str };
                beat.AddNote(note);
            }
            note.Fret = fret;
            note.IsTieDestination = false;
            note.IsDead = false;
            beat.IsEmpty = false;
            _anchor = null;
        });
    }

    /// <summary>Mette o toglie una nota (usato dalla tastiera del manico).</summary>
    public void ToggleFretAt(int line, int fret)
    {
        if (!IsEditable || CurrentBeat is not { } beat) return;
        Line = Math.Clamp(line, 1, StringCount);
        var existing = beat.GetNoteOnString(ModelString(Line));
        if (existing != null && (int)existing.Fret == fret)
            EditLocal(() => beat.RemoveNote(existing));
        else
            SetFret(fret);
    }

    /// <summary>Canc: con la selezione elimina i tempi; altrimenti toglie la nota, o il tempo se è vuoto.</summary>
    public void DeleteAtCursor()
    {
        if (!IsEditable || CurrentBeat is not { } beat) return;
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }
        var note = CurrentNote;
        if (note != null)
            EditLocal(() => beat.RemoveNote(note));
        else
            DeleteBeat();
    }

    public void MakeRest()
    {
        if (!IsEditable) return;
        var beats = SelectedBeats().Where(b => b.Notes.Count > 0).ToList();
        if (beats.Count == 0) return;
        EditSelected(() =>
        {
            foreach (var beat in beats)
                foreach (var n in beat.Notes.ToList()) beat.RemoveNote(n);
        });
    }

    /// <summary>
    /// Attiva/disattiva un effetto. Con la selezione vale per tutte le note selezionate,
    /// altrimenti per la nota sotto il cursore (o per il tempo, se è un effetto di tempo).
    /// </summary>
    public bool ToggleEffect(NoteEffect effect)
    {
        if (!IsEditable) return false;

        if (BeatEffects.Contains(effect))
        {
            var beats = SelectedBeats().Where(b => b.Notes.Count > 0).ToList();
            if (beats.Count == 0) return false;
            var on = !HasEffect(beats[0], null, effect);
            EditSelected(() => { foreach (var b in beats) SetBeatEffect(b, effect, on); });
            return true;
        }

        var notes = HasSelection
            ? SelectedBeats().SelectMany(b => b.Notes).ToList()
            : CurrentNote is { } n ? new List<Note> { n } : new List<Note>();
        if (notes.Count == 0) return false;
        var enable = !HasEffect(notes[0].Beat, notes[0], effect);
        EditSelected(() => { foreach (var note in notes) SetNoteEffect(note, effect, enable); });
        return true;
    }

    /// <summary>
    /// Hammer-on, slide e legatura collegano due note sulla stessa corda. Se l'altra nota manca,
    /// alphaTab scarta l'effetto in silenzio (in Score.Finish): va controllato PRIMA di applicarlo.
    /// Restituisce l'avviso da mostrare all'utente, oppure null se l'effetto si può mettere
    /// (o se lo si sta togliendo).
    /// </summary>
    public string? LinkWarning(NoteEffect effect)
    {
        if (HasSelection || CurrentNote is not { } note || HasEffect(note.Beat, note, effect)) return null;
        switch (effect)
        {
            case NoteEffect.HammerPull:
            case NoteEffect.Slide:
            case NoteEffect.SlideShift:
                if (HasNoteOnSameString(note, forward: true)) return null;
                var what = effect == NoteEffect.HammerPull ? "L'hammer-on / pull-off" : "Lo slide";
                return $"{what} parte da questa nota e arriva alla nota successiva sulla stessa corda, che qui manca.\n" +
                       "Scrivi prima la nota di arrivo in un tempo dopo, su questa corda, poi torna qui e riprova.";
            case NoteEffect.Tie:
                if (HasNoteOnSameString(note, forward: false)) return null;
                return "La legatura unisce questa nota a quella precedente sulla stessa corda, che qui manca.\n" +
                       "Va messa sulla seconda delle due note: scrivi prima la nota di partenza.";
            default:
                return null;
        }
    }

    private static bool HasNoteOnSameString(Note note, bool forward)
    {
        var beat = note.Beat;
        for (var i = 0; i < 32 && beat != null; i++)
        {
            beat = forward ? beat.NextBeat : beat.PreviousBeat;
            if (beat?.GetNoteOnString(note.String) != null) return true;
        }
        return false;
    }

    private static void SetBeatEffect(Beat beat, NoteEffect effect, bool on)
    {
        switch (effect)
        {
            case NoteEffect.Tap:
                beat.Tap = on;
                break;
            case NoteEffect.PickUp:
                beat.PickStroke = on ? PickStroke.Up : PickStroke.None;
                break;
            case NoteEffect.PickDown:
                beat.PickStroke = on ? PickStroke.Down : PickStroke.None;
                break;
            case NoteEffect.Tremolo:
                beat.TremoloPicking = on ? new TremoloPickingEffect { Marks = 2 } : null!;
                break;
        }
    }

    private static void SetNoteEffect(Note note, NoteEffect effect, bool on)
    {
        switch (effect)
        {
            case NoteEffect.HammerPull: note.IsHammerPullOrigin = on; break;
            case NoteEffect.Slide: note.SlideOutType = on ? SlideOutType.Legato : SlideOutType.None; break;
            case NoteEffect.SlideShift: note.SlideOutType = on ? SlideOutType.Shift : SlideOutType.None; break;
            case NoteEffect.SlideOutDown: note.SlideOutType = on ? SlideOutType.OutDown : SlideOutType.None; break;
            case NoteEffect.SlideInBelow: note.SlideInType = on ? SlideInType.IntoFromBelow : SlideInType.None; break;
            case NoteEffect.Bend: SetBend(note, on, BendType.Bend, (0, 0), (15, 4), (60, 4)); break;
            case NoteEffect.BendHalf: SetBend(note, on, BendType.Bend, (0, 0), (15, 2), (60, 2)); break;
            case NoteEffect.BendRelease: SetBend(note, on, BendType.BendRelease, (0, 0), (15, 4), (30, 4), (45, 0), (60, 0)); break;
            case NoteEffect.PrebendRelease: SetBend(note, on, BendType.PrebendRelease, (0, 4), (30, 4), (60, 0)); break;
            case NoteEffect.Vibrato: note.Vibrato = on ? VibratoType.Slight : VibratoType.None; break;
            case NoteEffect.VibratoWide: note.Vibrato = on ? VibratoType.Wide : VibratoType.None; break;
            case NoteEffect.PalmMute: note.IsPalmMute = on; break;
            case NoteEffect.Dead: note.IsDead = on; break;
            case NoteEffect.Ghost: note.IsGhost = on; break;
            case NoteEffect.LetRing: note.IsLetRing = on; break;
            case NoteEffect.Tie: note.IsTieDestination = on; break;
            case NoteEffect.Staccato: note.IsStaccato = on; break;
            case NoteEffect.Accent: note.Accentuated = on ? AccentuationType.Normal : AccentuationType.None; break;
            case NoteEffect.HeavyAccent: note.Accentuated = on ? AccentuationType.Heavy : AccentuationType.None; break;
            case NoteEffect.HarmonicNatural: SetHarmonic(note, on, HarmonicType.Natural, 0); break;
            case NoteEffect.HarmonicArtificial: SetHarmonic(note, on, HarmonicType.Artificial, 12); break;
            case NoteEffect.HarmonicPinch: SetHarmonic(note, on, HarmonicType.Pinch, 12); break;
            case NoteEffect.LeftHandTap: note.IsLeftHandTapped = on; break;
        }
    }

    private static void SetHarmonic(Note note, bool on, HarmonicType type, double value)
    {
        note.HarmonicType = on ? type : HarmonicType.None;
        note.HarmonicValue = on ? (type == HarmonicType.Natural ? note.Fret : note.Fret + value) : 0;
    }

    private static void SetBend(Note note, bool on, BendType type, params (double Offset, double Value)[] points)
    {
        note.BendPoints?.Clear();
        note.BendType = BendType.None;
        if (!on) return;
        note.BendType = type;
        foreach (var (o, v) in points) note.AddBendPoint(new BendPoint(o, v));
    }

    public static bool HasEffect(Beat? beat, Note? note, NoteEffect effect)
    {
        switch (effect)
        {
            case NoteEffect.Tap: return beat?.Tap == true;
            case NoteEffect.PickUp: return beat?.PickStroke == PickStroke.Up;
            case NoteEffect.PickDown: return beat?.PickStroke == PickStroke.Down;
            case NoteEffect.Tremolo: return beat?.IsTremolo == true;
        }
        if (note == null) return false;
        var maxBend = note.HasBend ? note.BendPoints!.Max(p => p.Value) : 0;
        return effect switch
        {
            NoteEffect.HammerPull => note.IsHammerPullOrigin,
            NoteEffect.Slide => note.SlideOutType == SlideOutType.Legato,
            NoteEffect.SlideShift => note.SlideOutType == SlideOutType.Shift,
            NoteEffect.SlideOutDown => note.SlideOutType == SlideOutType.OutDown,
            NoteEffect.SlideInBelow => note.SlideInType == SlideInType.IntoFromBelow,
            NoteEffect.Bend => note.BendType == BendType.Bend && maxBend >= 4,
            NoteEffect.BendHalf => note.BendType == BendType.Bend && maxBend is > 0 and < 4,
            NoteEffect.BendRelease => note.BendType == BendType.BendRelease,
            NoteEffect.PrebendRelease => note.BendType == BendType.PrebendRelease,
            NoteEffect.Vibrato => note.Vibrato == VibratoType.Slight,
            NoteEffect.VibratoWide => note.Vibrato == VibratoType.Wide,
            NoteEffect.PalmMute => note.IsPalmMute,
            NoteEffect.Dead => note.IsDead,
            NoteEffect.Ghost => note.IsGhost,
            NoteEffect.LetRing => note.IsLetRing,
            NoteEffect.Tie => note.IsTieDestination,
            NoteEffect.Staccato => note.IsStaccato,
            NoteEffect.Accent => note.Accentuated == AccentuationType.Normal,
            NoteEffect.HeavyAccent => note.Accentuated == AccentuationType.Heavy,
            NoteEffect.HarmonicNatural => note.HarmonicType == HarmonicType.Natural,
            NoteEffect.HarmonicArtificial => note.HarmonicType == HarmonicType.Artificial,
            NoteEffect.HarmonicPinch => note.HarmonicType == HarmonicType.Pinch,
            NoteEffect.LeftHandTap => note.IsLeftHandTapped,
            _ => false
        };
    }

    // ---------------------------------------------------------------- durate

    public void SetDuration(Duration duration)
    {
        CurrentDuration = duration;
        if (!IsEditable) return;
        var beats = SelectedBeats().Where(b => b.Duration != duration).ToList();
        if (beats.Count == 0) { CursorMoved?.Invoke(); return; }
        EditSelected(() => { foreach (var b in beats) b.Duration = duration; });
    }

    public void LongerDuration() => StepDuration(-1);
    public void ShorterDuration() => StepDuration(+1);

    private void StepDuration(int delta)
    {
        var current = CurrentBeat?.Duration ?? CurrentDuration;
        var i = Array.IndexOf(Durations, current);
        if (i < 0) i = 2;
        SetDuration(Durations[Math.Clamp(i + delta, 0, Durations.Length - 1)]);
    }

    public void ToggleDot()
    {
        var beats = SelectedBeats();
        if (!IsEditable || beats.Count == 0) return;
        var on = beats[0].Dots == 0;
        EditSelected(() => { foreach (var b in beats) b.Dots = on ? 1 : 0; });
    }

    /// <summary>Terzina (3 nel tempo di 2), o altro gruppo irregolare.</summary>
    public void ToggleTuplet(int numerator = 3, int denominator = 2)
    {
        var beats = SelectedBeats();
        if (!IsEditable || beats.Count == 0) return;
        var on = !(beats[0].TupletNumerator == numerator && beats[0].TupletDenominator == denominator);
        EditSelected(() =>
        {
            foreach (var b in beats)
            {
                b.TupletNumerator = on ? numerator : -1;
                b.TupletDenominator = on ? denominator : -1;
            }
        });
    }

    public static string TupletLabel(Beat? beat) =>
        beat is { HasTuplet: true } ? $"{beat.TupletNumerator}:{beat.TupletDenominator}" : "";

    // ---------------------------------------------------------------- tempi e battute

    public void InsertBeat()
    {
        if (!IsEditable || CurrentVoice is not { } voice) return;
        EditLocal(() =>
        {
            var b = NewRest(CurrentDuration);
            b.Voice = voice;
            voice.Beats.Insert(BeatIndex, b);
            Relink();
            _anchor = null;
        });
    }

    public void DeleteBeat()
    {
        if (!IsEditable || CurrentVoice is not { } voice || CurrentBeat == null) return;
        if (voice.Beats.Count == 1)
        {
            MakeRest();
            return;
        }
        EditLocal(() =>
        {
            voice.Beats.RemoveAt(BeatIndex);
            Relink();
            if (BeatIndex >= voice.Beats.Count) BeatIndex = voice.Beats.Count - 1;
            _anchor = null;
        });
    }

    public void InsertBarAfter()
    {
        if (!IsEditable) return;
        Edit(() =>
        {
            InsertBarAt(BarIndex + 1);
            BarIndex++;
            BeatIndex = 0;
            _anchor = null;
        }, new HistorySnapshot.BarSplice(Score!, BarIndex + 1, 0, 1));
    }

    public void DeleteBar()
    {
        if (!IsEditable || Score == null || Score.MasterBars.Count <= 1) return;
        Edit(() =>
        {
            RemoveBarAt(BarIndex);
            Relink();
            BeatIndex = 0;
            _anchor = null;
        }, new HistorySnapshot.BarSplice(Score!, BarIndex, 1, 0));
    }

    /// <summary>Riempie di pause la parte mancante della battuta, o toglie i tempi in eccesso.</summary>
    public void FixBar()
    {
        if (!IsEditable || CurrentVoice is not { } voice) return;
        if (!BarFill(out var used, out var total) || Math.Abs(used - total) < 0.5) return;
        EditLocal(() =>
        {
            if (used > total)
            {
                while (voice.Beats.Count > 1 && voice.CalculateDuration() > total + 0.5)
                {
                    voice.Beats.RemoveAt(voice.Beats.Count - 1);
                    Relink();
                    Score!.Finish(_settings);
                }
            }
            // Una sola nota può essere più lunga dell'intera battuta: ne conserva il tasto,
            // riduce la durata e completa con pause, come dopo la rimozione dei tempi in eccesso.
            if (voice.CalculateDuration() > total + 0.5)
            {
                var beat = voice.Beats[0];
                beat.Duration = Durations.First(d => Ticks(d) <= total + 0.5);
                beat.Dots = 0;
                beat.TupletNumerator = beat.TupletDenominator = -1;
                Score!.Finish(_settings);
            }
            foreach (var rest in RestsForTicks(total - voice.CalculateDuration(), Duration.Whole))
                voice.AddBeat(rest);
            Relink();
        });
    }

    /// <summary>Quanto è piena la battuta corrente (in tick) rispetto al tempo in chiave.</summary>
    public bool BarFill(out double used, out double total)
    {
        used = total = 0;
        if (CurrentVoice is not { } voice || CurrentBar is not { } bar) return false;
        used = voice.CalculateDuration();
        total = bar.MasterBar.CalculateDuration(false);
        return true;
    }

    private void RemoveBarAt(int index)
    {
        Score!.MasterBars.RemoveAt(index);
        foreach (var track in Score.Tracks)
            foreach (var staff in track.Staves)
                if (index < staff.Bars.Count) staff.Bars.RemoveAt(index);
    }

    private void InsertBarAt(int index)
    {
        var score = Score!;
        var template = score.MasterBars[Math.Clamp(index - 1, 0, score.MasterBars.Count - 1)];
        var mb = new MasterBar
        {
            TimeSignatureNumerator = template.TimeSignatureNumerator,
            TimeSignatureDenominator = template.TimeSignatureDenominator,
            TimeSignatureCommon = template.TimeSignatureCommon,
            Score = score
        };
        // Nota: l'armatura di chiave si copia sulle singole Bar qui sotto; il setter di MasterBar
        // scrive sulle battute collegate, che qui non esistono ancora.
        score.MasterBars.Insert(index, mb);

        foreach (var track in score.Tracks)
        {
            foreach (var staff in track.Staves)
            {
                var prev = staff.Bars[Math.Clamp(index - 1, 0, staff.Bars.Count - 1)];
                var bar = new Bar
                {
                    Clef = prev.Clef,
                    ClefOttava = prev.ClefOttava,
                    KeySignature = prev.KeySignature,
                    KeySignatureType = prev.KeySignatureType,
                    Staff = staff
                };
                staff.Bars.Insert(index, bar);

                var voices = Math.Max(track.Index == TrackIndex ? VoiceIndex + 1 : 1, prev.Voices.Count);
                for (var v = 0; v < voices; v++)
                {
                    var voice = new Voice();
                    bar.AddVoice(voice);
                    if (v == 0 || (track.Index == TrackIndex && v == VoiceIndex))
                    {
                        var duration = track.Index == TrackIndex ? CurrentDuration : Duration.Quarter;
                        foreach (var r in RestsFor(mb, duration)) voice.AddBeat(r);
                    }
                    else
                    {
                        var rest = NewRest(Duration.Whole);
                        rest.IsEmpty = true;
                        voice.AddBeat(rest);
                    }
                }
            }
        }
        Relink();
    }

    /// <summary>Pause della durata data che riempiono esattamente una battuta.</summary>
    private static IEnumerable<Beat> RestsFor(MasterBar mb, Duration duration)
    {
        var total = 3840.0 * mb.TimeSignatureNumerator / mb.TimeSignatureDenominator;
        return RestsForTicks(total, duration);
    }

    private static IEnumerable<Beat> RestsForTicks(double remaining, Duration preferred)
    {
        foreach (var duration in Durations.Where(d => Ticks(d) <= Ticks(preferred)))
        {
            while (remaining >= Ticks(duration) - 0.5)
            {
                yield return NewRest(duration);
                remaining -= Ticks(duration);
            }
        }
    }

    private static Beat NewRest(Duration d) => new() { Duration = d, IsEmpty = false };

    private static double Ticks(Duration d) => (int)d > 0 ? 3840.0 / (int)d : 3840.0;

    /// <summary>Ricostruisce indici e collegamenti dopo inserimenti o cancellazioni.</summary>
    private void Relink()
    {
        var score = Score!;
        for (var i = 0; i < score.MasterBars.Count; i++)
        {
            var mb = score.MasterBars[i];
            mb.Index = i;
            mb.Score = score;
            mb.PreviousMasterBar = i > 0 ? score.MasterBars[i - 1] : null!;
            mb.NextMasterBar = i < score.MasterBars.Count - 1 ? score.MasterBars[i + 1] : null!;
        }

        for (var t = 0; t < score.Tracks.Count; t++)
        {
            var track = score.Tracks[t];
            track.Index = t;
            track.Score = score;
            for (var s = 0; s < track.Staves.Count; s++)
            {
                var staff = track.Staves[s];
                staff.Index = s;
                staff.Track = track;
                var lastBeats = new Dictionary<int, Beat>();
                for (var i = 0; i < staff.Bars.Count; i++)
                {
                    var bar = staff.Bars[i];
                    bar.Index = i;
                    bar.Staff = staff;
                    bar.PreviousBar = i > 0 ? staff.Bars[i - 1] : null!;
                    bar.NextBar = i < staff.Bars.Count - 1 ? staff.Bars[i + 1] : null!;

                    for (var v = 0; v < bar.Voices.Count; v++)
                    {
                        var voice = bar.Voices[v];
                        voice.Index = v;
                        voice.Bar = bar;
                        for (var b = 0; b < voice.Beats.Count; b++)
                        {
                            var beat = voice.Beats[b];
                            beat.Index = b;
                            beat.Voice = voice;
                            beat.PreviousBeat = null!;
                            beat.NextBeat = null!;
                        }
                    }

                    // Ogni voce ha la propria catena, anche tra battute (legature, hammer, slide).
                    for (var v = 0; v < bar.Voices.Count; v++)
                    {
                        foreach (var beat in bar.Voices[v].Beats)
                        {
                            if (lastBeats.TryGetValue(v, out var lastBeat))
                            {
                                lastBeat.NextBeat = beat;
                                beat.PreviousBeat = lastBeat;
                            }
                            lastBeats[v] = beat;
                        }
                    }
                }
            }
        }
        // Anche le battute senza segni di ritornello devono appartenere a un RepeatGroup:
        // il lettore MIDI lo usa per attraversarle dopo inserimenti, incolla e cancellazioni.
        score.RebuildRepeatGroups();
    }

    // ---------------------------------------------------------------- proprietà della battuta

    public BarInfo GetBarInfo()
    {
        var mb = Score!.MasterBars[BarIndex];
        var change = mb.TempoAutomations.FirstOrDefault(a => a.Type == AutomationType.Tempo);
        return new BarInfo(
            change?.Value,
            TempoAt(BarIndex),
            (int)mb.TimeSignatureNumerator,
            (int)mb.TimeSignatureDenominator,
            mb.IsRepeatStart,
            (int)mb.RepeatCount,
            mb.Section?.Text);
    }

    public double TempoAt(int barIndex)
    {
        if (Score == null) return 120;
        for (var i = Math.Min(barIndex, Score.MasterBars.Count - 1); i >= 0; i--)
        {
            var a = Score.MasterBars[i].TempoAutomations.LastOrDefault(x => x.Type == AutomationType.Tempo);
            if (a != null) return a.Value;
        }
        return Score.Tempo > 0 ? Score.Tempo : 120;
    }

    public void SetBarInfo(BarInfo info)
    {
        if (Score == null) return;
        Edit(() =>
        {
            var mb = Score.MasterBars[BarIndex];

            // Tempo (BPM): un cambio su questa battuta, oppure nessuno.
            var old = mb.TempoAutomations.Where(a => a.Type == AutomationType.Tempo).ToList();
            foreach (var a in old) mb.TempoAutomations.Remove(a);
            var bpm = info.TempoChange ?? (BarIndex == 0 ? TempoAt(0) : (double?)null);
            if (bpm is > 0)
                mb.TempoAutomations.Add(Automation.BuildTempoAutomation(false, 0, Math.Clamp(bpm.Value, 20, 400), 0, true));

            // Tempo in chiave: da qui fino alla prossima battuta che aveva già un tempo diverso.
            var oldNum = mb.TimeSignatureNumerator;
            var oldDen = mb.TimeSignatureDenominator;
            if (info.Numerator != (int)oldNum || info.Denominator != (int)oldDen)
            {
                for (var i = BarIndex; i < Score.MasterBars.Count; i++)
                {
                    var m = Score.MasterBars[i];
                    if (i > BarIndex && (m.TimeSignatureNumerator != oldNum || m.TimeSignatureDenominator != oldDen)) break;
                    m.TimeSignatureNumerator = Math.Clamp(info.Numerator, 1, 32);
                    m.TimeSignatureDenominator = info.Denominator;
                    m.TimeSignatureCommon = false;
                }
            }

            mb.IsRepeatStart = info.RepeatStart;
            mb.RepeatCount = Math.Max(0, info.RepeatCount);
            mb.Section = string.IsNullOrWhiteSpace(info.Section) ? null! : new Section { Text = info.Section.Trim(), Marker = "" };

            Score.RebuildRepeatGroups();
        });
    }

    // ---------------------------------------------------------------- tracce

    public void SetTrackProperties(string name, int program, IList<double> tuning, string tuningName, int capo)
    {
        if (Track is not { } track || Staff is not { } staff) return;
        Edit(() =>
        {
            track.Name = name;
            track.ShortName = name.Length > 6 ? name[..6] : name;
            track.PlaybackInfo.Program = program;
            if (tuning.Count == staff.Tuning.Count)
            {
                var preset = Tuning.FindTuning(tuning);
                staff.StringTuning = new Tuning(preset?.Name ?? tuningName, tuning.ToList(), preset?.IsStandard ?? false);
            }
            staff.Capo = Math.Clamp(capo, 0, 12);
        });
        TracksChanged?.Invoke();
    }

    /// <summary>Aggiunge una traccia a corde (chitarra, basso…) con battute di pause.</summary>
    public void AddTrack(string name, int program, IList<double> tuning, bool isBass)
    {
        if (Score == null) return;
        Edit(() =>
        {
            var track = new Track { Name = name, ShortName = name.Length > 6 ? name[..6] : name };
            var (primary, secondary) = FreeChannels();
            track.PlaybackInfo.Program = program;
            track.PlaybackInfo.Volume = 15;
            track.PlaybackInfo.Balance = 8;
            track.PlaybackInfo.PrimaryChannel = primary;
            track.PlaybackInfo.SecondaryChannel = secondary;

            var staff = new Staff
            {
                ShowTablature = true,
                ShowStandardNotation = true,
                // Chitarra e basso si scrivono un'ottava sopra il suono reale.
                DisplayTranspositionPitch = -12
            };
            var preset = Tuning.FindTuning(tuning);
            staff.StringTuning = new Tuning(preset?.Name ?? "", tuning.ToList(), preset?.IsStandard ?? false);
            track.AddStaff(staff);

            foreach (var mb in Score.MasterBars)
            {
                var bar = new Bar { Clef = isBass ? Clef.F4 : Clef.G2 };
                staff.AddBar(bar);
                var voice = new Voice();
                bar.AddVoice(voice);
                foreach (var r in RestsFor(mb, Duration.Quarter)) voice.AddBeat(r);
            }

            Score.AddTrack(track);
            Relink();
            TrackIndex = Score.Tracks.Count - 1;
            BarIndex = BeatIndex = 0;
            Line = 1;
            _anchor = null;
        });
        TracksChanged?.Invoke();
    }

    public void DeleteTrack()
    {
        if (Score == null || Score.Tracks.Count <= 1) return;
        Edit(() =>
        {
            Score.Tracks.RemoveAt(TrackIndex);
            Relink();
            TrackIndex = Math.Min(TrackIndex, Score.Tracks.Count - 1);
            _anchor = null;
        });
        TracksChanged?.Invoke();
    }

    private (int, int) FreeChannels()
    {
        var used = new HashSet<int>();
        foreach (var t in Score!.Tracks)
        {
            used.Add((int)t.PlaybackInfo.PrimaryChannel);
            used.Add((int)t.PlaybackInfo.SecondaryChannel);
        }
        var free = Enumerable.Range(0, 16).Where(c => c != DrumChannel && !used.Contains(c)).ToList();
        return free.Count >= 2 ? (free[0], free[1]) : (0, 1);
    }

    // ---------------------------------------------------------------- annulla / ripeti

    public void Undo()
    {
        if (_undo.Count == 0 || Score == null) return;
        _redo.Add(_undo[^1].CaptureCurrent(this));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    public void Redo()
    {
        if (_redo.Count == 0 || Score == null) return;
        _undo.Add(_redo[^1].CaptureCurrent(this));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    private void Restore(HistorySnapshot snapshot)
    {
        var trackCount = Score?.Tracks.Count;
        snapshot.Restore(this);
        EnsureUniqueIds();
        ChangedBars = snapshot.BarIndices;
        IsDirty = true;
        _anchor = null;
        ClampCursor();
        Changed?.Invoke();
        if (trackCount != Score!.Tracks.Count) TracksChanged?.Invoke();
    }

    internal void RestoreScore(byte[] bytes) => Score = Import(bytes);

    internal void FinishRestoredBars(Action chain)
    {
        Relink();
        chain();
        Score!.Finish(_settings);
    }

    private void EditSelected(Action change) => EditLocal(change, SelectedBeats().Select(b => b.Voice.Bar));

    private void EditLocal(Action change, IEnumerable<Bar>? bars = null) =>
        Edit(change, HistorySnapshot.Bars.Capture(bars ?? [CurrentBar!]));

    /// <summary>Esegue una modifica: salva lo stato per l'annulla, applica, ricalcola il modello.</summary>
    private void Edit(Action change, HistorySnapshot? snapshot = null)
    {
        if (Score == null) return;
        _undo.Add(snapshot ?? new HistorySnapshot.Full(ExportGp()));
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        _redo.Clear();

        change();

        RemoveDetachedNoteLinks();
        Score.Finish(_settings);
        EnsureUniqueIds();
        ChangedBars = snapshot?.BarIndices;
        IsDirty = true;
        ClampCursor();
        Changed?.Invoke();
    }

    private void RemoveDetachedNoteLinks()
    {
        var notes = Score!.Tracks.SelectMany(t => t.Staves).SelectMany(s => s.Bars)
            .SelectMany(b => b.Voices).SelectMany(v => v.Beats).SelectMany(b => b.Notes).ToHashSet();
        Note? Live(Note? note) => note != null && notes.Contains(note) ? note : null;
        foreach (var note in notes)
        {
            note.SlideTarget = Live(note.SlideTarget);
            note.SlideOrigin = Live(note.SlideOrigin);
            note.TieOrigin = Live(note.TieOrigin);
            note.TieDestination = Live(note.TieDestination);
            note.HammerPullOrigin = Live(note.HammerPullOrigin);
            note.HammerPullDestination = Live(note.HammerPullDestination);
            note.SlurOrigin = Live(note.SlurOrigin);
            note.SlurDestination = Live(note.SlurDestination);
            note.EffectSlurOrigin = Live(note.EffectSlurOrigin);
            note.EffectSlurDestination = Live(note.EffectSlurDestination);
            note.BendOrigin = Live(note.BendOrigin);
            note.LetRingDestination = Live(note.LetRingDestination);
            note.PalmMuteDestination = Live(note.PalmMuteDestination);
        }
    }

    // Gli importer alphaTab azzerano i contatori statici degli ID. Dopo un ripristino locale
    // possono quindi coesistere vecchi ID alti e nuove note con ID già presenti.
    // I riferimenti del modello restano oggetti; correggi solo le collisioni prima del rendering.
    private void EnsureUniqueIds()
    {
        var bars = Score!.Tracks.SelectMany(t => t.Staves).SelectMany(s => s.Bars).ToList();
        var voices = bars.SelectMany(b => b.Voices).ToList();
        var beats = voices.SelectMany(v => v.Beats).ToList();
        Unique(bars, b => b.Id, (b, id) => b.Id = id);
        Unique(voices, v => v.Id, (v, id) => v.Id = id);
        Unique(beats, b => b.Id, (b, id) => b.Id = id);
        Unique(beats.SelectMany(b => b.Notes).ToList(), n => n.Id, (n, id) => n.Id = id);
    }

    private static void Unique<T>(IList<T> items, Func<T, double> get, Action<T, double> set)
    {
        var seen = new HashSet<double>();
        var next = items.Count == 0 ? 0 : items.Max(get) + 1;
        foreach (var item in items)
            if (!seen.Add(get(item))) { set(item, next); seen.Add(next++); }
    }

    /// <summary>Velocità iniziale del brano (BPM della prima battuta).</summary>
    public void SetStartTempo(double bpm)
    {
        if (Score == null) return;
        Edit(() =>
        {
            var mb = Score.MasterBars[0];
            foreach (var a in mb.TempoAutomations.Where(a => a.Type == AutomationType.Tempo).ToList())
                mb.TempoAutomations.Remove(a);
            mb.TempoAutomations.Add(Automation.BuildTempoAutomation(false, 0, Math.Clamp(bpm, 20, 400), 0, true));
        });
    }

    public void SetTitle(string title)
    {
        if (Score == null || Score.Title == title) return;
        Edit(() => Score.Title = title);
    }

    public void SetArtist(string artist)
    {
        if (Score == null || Score.Artist == artist) return;
        Edit(() => Score.Artist = artist);
    }
}
