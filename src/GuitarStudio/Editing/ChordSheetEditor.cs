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
