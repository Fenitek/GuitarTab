using System.Collections.Immutable;

namespace GuitarStudio.Editing;

/// <summary>
/// Logica del foglio di accordi, senza interfaccia (come <see cref="TabEditor"/>). Il foglio è
/// immutabile: ogni modifica sostituisce <see cref="Sheet"/> e la versione precedente va nella
/// cronologia di annulla. La scrittura continua nella stessa sezione si raccoglie in un solo passo.
/// </summary>
public sealed class ChordSheetEditor
{
    public const int MaxUndo = 200;
    public const int MaxSections = 60;

    private readonly List<(ChordSheet Sheet, string? Group)> _undo = new();
    private readonly List<(ChordSheet Sheet, string? Group)> _redo = new();
    private ChordSheet? _saved; // null: mai salvato in questa forma (recupero)
    private string? _openGroup;

    public ChordSheetEditor() : this(ChordSheet.New()) { }

    public ChordSheetEditor(ChordSheet sheet)
    {
        Sheet = sheet;
        _saved = sheet;
    }

    public ChordSheet Sheet { get; private set; }

    /// <summary>Aumenta a ogni cambiamento del foglio (modifica, annulla, ripeti, caricamento).</summary>
    public int Revision { get; private set; }

    public bool IsDirty => _saved == null || !Sheet.SameContent(_saved);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Il foglio è cambiato. L'argomento è true quando il cambiamento viene da annulla/ripeti o da un caricamento.</summary>
    public event Action<bool>? Changed;

    // ---------------------------------------------------------------- documento

    /// <summary>Sostituisce il foglio e azzera la cronologia (nuovo, apri, recupero).</summary>
    public void Load(ChordSheet sheet, bool isDirty = false)
    {
        Sheet = sheet;
        _saved = isDirty ? null : sheet;
        _undo.Clear();
        _redo.Clear();
        _openGroup = null;
        Revision++;
        Changed?.Invoke(true);
    }

    public void MarkSaved()
    {
        _saved = Sheet;
        _openGroup = null;
    }

    /// <summary>Chiude il gruppo di scrittura in corso: il prossimo carattere sarà un passo di annulla a sé.</summary>
    public void EndTyping() => _openGroup = null;

    // ---------------------------------------------------------------- informazioni

    public bool SetInfo(string title, string artist, string key, int capo)
    {
        capo = Math.Clamp(capo, 0, ChordSheetText.MaxCapo);
        return Apply(Sheet with { Title = title.Trim(), Artist = artist.Trim(), Key = key.Trim(), Capo = capo });
    }

    // ---------------------------------------------------------------- sezioni

    /// <summary>Aggiunge una sezione dopo <paramref name="after"/> (in fondo se -1 o fuori). Restituisce l'indice, -1 se non si può.</summary>
    public int AddSection(string name, SheetSectionKind kind, int after = -1)
    {
        if (Sheet.Sections.Count >= MaxSections) return -1;
        var index = after < 0 || after >= Sheet.Sections.Count ? Sheet.Sections.Count : after + 1;
        var section = new SheetSection(CleanName(name, kind), kind, "");
        Apply(Sheet with { Sections = Sheet.Sections.Insert(index, section) });
        return index;
    }

    public bool RemoveSection(int index) =>
        InRange(index) && Apply(Sheet with { Sections = Sheet.Sections.RemoveAt(index) });

    public bool RenameSection(int index, string name) =>
        InRange(index) && Apply(Sheet with { Sections = Sheet.Sections.SetItem(index, Sheet.Sections[index] with { Name = CleanName(name, Sheet.Sections[index].Kind) }) });

    /// <summary>Sposta la sezione su (-1) o giù (+1). Restituisce il nuovo indice, o quello vecchio se non si muove.</summary>
    public int MoveSection(int index, int delta)
    {
        var target = index + delta;
        if (!InRange(index) || !InRange(target) || delta == 0) return index;
        var section = Sheet.Sections[index];
        Apply(Sheet with { Sections = Sheet.Sections.RemoveAt(index).Insert(target, section) });
        return target;
    }

    /// <summary>Cambia il tipo. Il corpo resta: le parentesi di un testo diventano accordi del giro e viceversa.</summary>
    public bool SetSectionKind(int index, SheetSectionKind kind)
    {
        if (!InRange(index) || Sheet.Sections[index].Kind == kind) return false;
        var section = Sheet.Sections[index];
        var body = kind == SheetSectionKind.Progression
            ? string.Join("\n", ChordSheetText.Lines(section.Body)
                .Select(l => ChordSheetText.ParseLyric(l).Chords.Select(c => c.Name).ToList())
                .Where(c => c.Count > 0)
                .Select(c => ChordSheetText.FormatRow(c.Select(n => (IReadOnlyList<string>)new[] { n }))))
            : string.Join("\n", ChordSheetText.Lines(section.Body)
                .Select(l => string.Concat(ChordSheetText.ParseRow(l).SelectMany(b => b).Select(n => $"[{n}] "))).Select(l => l.TrimEnd()));
        return Apply(Sheet with { Sections = Sheet.Sections.SetItem(index, section with { Kind = kind, Body = body }) });
    }

    /// <summary>
    /// Testo scritto nella sezione. Con <paramref name="typing"/> i caratteri consecutivi nella
    /// stessa sezione formano un solo passo di annulla, fino a <see cref="EndTyping"/>.
    /// </summary>
    public bool SetSectionBody(int index, string body, bool typing = false)
    {
        if (!InRange(index)) return false;
        body = body.Replace("\r\n", "\n");
        var group = typing ? "body:" + index : null;
        return Apply(Sheet with { Sections = Sheet.Sections.SetItem(index, Sheet.Sections[index] with { Body = body }) }, group);
    }

    /// <summary>
    /// Inserisce l'accordo nel punto <paramref name="offset"/> del corpo (posizione del cursore di testo).
    /// Nei testi diventa «[Nome]» davanti alla sillaba; nei giri si aggiunge come parola con gli spazi
    /// necessari. Restituisce la nuova posizione del cursore, oppure -1 se il nome non è valido.
    /// </summary>
    public int InsertChord(int index, int offset, string name)
    {
        if (!InRange(index) || ChordSheetText.ChordNameProblem(name) != null) return -1;
        name = name.Trim();
        var section = Sheet.Sections[index];
        var body = section.Body.Replace("\r\n", "\n");
        offset = Math.Clamp(offset, 0, body.Length);
        // Mai dentro un'altra parentesi: l'accordo va subito dopo la parentesi che si chiude.
        if (offset > 0)
        {
            var lineStart = body.LastIndexOf('\n', offset - 1) + 1;
            var open = body.LastIndexOf('[', offset - 1, offset - lineStart);
            if (open >= 0)
            {
                var close = body.IndexOf(']', open);
                var lineEnd = body.IndexOf('\n', open);
                if (lineEnd < 0) lineEnd = body.Length;
                if (close >= offset && close < lineEnd) offset = close + 1;
            }
        }
        string insert;
        var caret = 0;
        if (section.Kind == SheetSectionKind.Lyrics)
        {
            insert = "[" + name + "]";
            caret = insert.Length;
        }
        else
        {
            var before = offset > 0 && !char.IsWhiteSpace(body[offset - 1]) ? " " : "";
            var after = offset < body.Length && !char.IsWhiteSpace(body[offset]) ? " " : "";
            insert = before + name + after;
            caret = before.Length + name.Length;
        }
        EndTyping();
        Apply(Sheet with { Sections = Sheet.Sections.SetItem(index, section with { Body = body.Insert(offset, insert) }) });
        EndTyping();
        return offset + caret;
    }

    // ---------------------------------------------------------------- trasposizione, capotasto, nomi

    /// <summary>
    /// Sposta tutti gli accordi e la tonalità di <paramref name="semitones"/>. Le alterazioni
    /// seguono la nuova tonalità (Mi +4 in Mi maggiore → La♭, perché La♭ maggiore ha i bemolle;
    /// in Mi minore → Sol♯); senza tonalità, o in Do/La minore, si sale con i diesis e si scende
    /// con i bemolle. Ogni accordo resta nella sua notazione; i nomi non riconosciuti non cambiano.
    /// </summary>
    public bool Transpose(int semitones)
    {
        if (semitones % 12 == 0) return false;
        var key = ChordName.Parse(Sheet.Key);
        var target = key?.TransposeKey(semitones);
        var spelling = SpellingFor(target, semitones);
        var next = MapChords(Sheet, c => c.Transpose(semitones, spelling));
        return Apply(target != null ? next with { Key = target.Format(key!.Notation) } : next);
    }

    /// <summary>
    /// Capotasto. Con <paramref name="keepSound"/> gli accordi diventano le forme da suonare con il
    /// capotasto (scendono di quanto sale il capotasto) e il brano suona uguale; la tonalità resta
    /// quella reale. Senza, cambia solo l'indicazione.
    /// </summary>
    public bool SetCapo(int capo, bool keepSound)
    {
        capo = Math.Clamp(capo, 0, ChordSheetText.MaxCapo);
        var delta = capo - Sheet.Capo;
        if (delta == 0) return false;
        var next = Sheet with { Capo = capo };
        if (keepSound)
        {
            // Le forme sono nella tonalità reale meno il capotasto.
            var shapesKey = ChordName.Parse(Sheet.Key)?.TransposeKey(-capo);
            var spelling = SpellingFor(shapesKey, -delta);
            next = MapChords(next, c => c.Transpose(-delta, spelling));
        }
        return Apply(next);
    }

    /// <summary>Riscrive accordi e tonalità con i nomi italiani (Do Re Mi) o internazionali (C D E).</summary>
    public bool ConvertNotation(ChordNotation notation)
    {
        var next = MapChords(Sheet, c => c with { Notation = notation });
        return Apply(ChordName.Parse(next.Key) is { } key ? next with { Key = (key with { Notation = notation }).ToString() } : next);
    }

    /// <summary>Notazione più usata dagli accordi del foglio (null se non ci sono accordi riconosciuti).</summary>
    public ChordNotation? DominantNotation()
    {
        var names = Sheet.Sections.SelectMany(ChordSheetText.ChordNames).Append(Sheet.Key)
            .Select(ChordName.Parse).OfType<ChordName>().ToList();
        if (names.Count == 0) return null;
        return names.Count(n => n.Notation == ChordNotation.Italian) * 2 >= names.Count ? ChordNotation.Italian : ChordNotation.International;
    }

    /// <summary>Diagramma scelto per un accordo («x 3 2 0 1 0»); null torna alla forma proposta.</summary>
    public bool SetDiagram(string name, IReadOnlyList<int?>? fretsLowToHigh)
    {
        name = name.Trim();
        if (name.Length == 0) return false;
        var diagrams = fretsLowToHigh == null
            ? Sheet.Diagrams.Remove(name)
            : Sheet.Diagrams.SetItem(name, TabEditor.FormatFrets(fretsLowToHigh));
        return Apply(Sheet with { Diagrams = diagrams });
    }

    private static Spelling SpellingFor(ChordName? key, int semitones) =>
        key != null && !key.IsNeutralKey ? key.KeySpelling() : semitones > 0 ? Spelling.Sharps : Spelling.Flats;

    private static ChordSheet MapChords(ChordSheet sheet, Func<ChordName, ChordName> map)
    {
        string Map(string name) => ChordName.Parse(name) is { } chord ? map(chord).ToString() : name;
        return sheet with { Sections = sheet.Sections.Select(s => s with { Body = ChordSheetText.MapChords(s, Map) }).ToImmutableList() };
    }

    // ---------------------------------------------------------------- annulla / ripeti

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var (previous, _) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add((Sheet, null));
        Sheet = previous;
        _openGroup = null;
        Revision++;
        Changed?.Invoke(true);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var (next, _) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add((Sheet, null));
        Sheet = next;
        _openGroup = null;
        Revision++;
        Changed?.Invoke(true);
        return true;
    }

    // ---------------------------------------------------------------- interno

    /// <summary>Unico punto di modifica: salva lo stato per annulla, svuota ripeti e avvisa.</summary>
    private bool Apply(ChordSheet next, string? group = null)
    {
        if (next.SameContent(Sheet)) return false;
        var merge = group != null && group == _openGroup && _undo.Count > 0;
        if (!merge)
        {
            _undo.Add((Sheet, group));
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }
        _redo.Clear();
        _openGroup = group;
        Sheet = next;
        Revision++;
        Changed?.Invoke(false);
        return true;
    }

    private bool InRange(int index) => index >= 0 && index < Sheet.Sections.Count;

    private static string CleanName(string name, SheetSectionKind kind)
    {
        name = name.Trim();
        if (name.Length > 40) name = name[..40];
        return name.Length > 0 ? name : kind == SheetSectionKind.Lyrics ? "Strofa" : "Giro";
    }
}
