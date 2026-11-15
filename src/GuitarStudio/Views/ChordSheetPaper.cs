using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Impaginazione di lettura del foglio di accordi sulla carta chiara: titolo, sezioni, accordi
/// sopra le sillabe e giri divisi in battute. Solo disegno: nessuna logica di modifica.
/// </summary>
public static class ChordSheetPaper
{
    /// <summary>Dimensioni a scala 1: testo 22 DIP, accordi 20 DIP (la vista di lettura parte da qui).</summary>
    public const double LyricSize = 22, ChordSize = 20;

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
    private static FontFamily Font(string key) => (FontFamily)Application.Current.FindResource(key);

    /// <param name="display">Come mostrare un nome d'accordo (notazione, simboli ♯ ♭).</param>
    /// <param name="diagrams">Diagrammi degli accordi usati in cima al foglio (null = nessuno).</param>
    /// <param name="onDiagramClick">In scrittura: clic su un diagramma (o su un accordo senza forma) per sceglierlo.</param>
    public static FrameworkElement Build(ChordSheet sheet, double scale, Func<string, string>? display = null,
        bool diagrams = false, Action<string>? onDiagramClick = null)
    {
        display ??= name => name;
        var root = new StackPanel { Margin = new Thickness(36 * scale, 28 * scale, 36 * scale, 40 * scale) };
        var title = string.IsNullOrWhiteSpace(sheet.Title) ? "Senza titolo" : sheet.Title;
        root.Children.Add(Text(title, 30 * scale, Res("SheetInk"), FontWeights.SemiBold));
        if (!string.IsNullOrWhiteSpace(sheet.Artist))
            root.Children.Add(Text(sheet.Artist, 18 * scale, Res("SheetMuted"), FontWeights.Normal));
        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(sheet.Key)) meta.Add("Tonalità " + display(sheet.Key));
        if (sheet.Capo > 0) meta.Add($"Capotasto al {sheet.Capo}° tasto");
        if (meta.Count > 0)
            root.Children.Add(Text(string.Join("  ·  ", meta), Math.Max(13, 15 * scale), Res("SheetMuted"), FontWeights.Normal, top: 6 * scale));

        if (diagrams && sheet.UsedChords().Count > 0) root.Children.Add(Diagrams(sheet, scale, display, onDiagramClick));

        foreach (var section in sheet.Sections)
        {
            var header = Text(section.Name, Math.Max(13, 15 * scale), Res("SheetMuted"), FontWeights.Bold, top: 26 * scale);
            header.Margin = new Thickness(0, 26 * scale, 0, 4 * scale);
            root.Children.Add(header);
            var lines = ChordSheetText.Lines(section.Body);
            if (lines.Length == 0)
            {
                root.Children.Add(Text(section.Kind == SheetSectionKind.Lyrics ? "(sezione vuota)" : "(giro vuoto)",
                    Math.Max(13, 15 * scale), Res("SheetMuted"), FontWeights.Normal));
                continue;
            }
            foreach (var line in lines)
                root.Children.Add(section.Kind == SheetSectionKind.Lyrics
                    ? LyricRow(ChordSheetText.ParseLyric(line), scale, display)
                    : ProgressionRow(ChordSheetText.ParseRow(line), scale, display));
        }
        return root;
    }

    /// <summary>
    /// Fila dei diagrammi degli accordi usati, nell'ordine in cui compaiono. In lettura gli accordi
    /// senza forma nota non compaiono; in scrittura c'è un riquadro «?» da cliccare per definirli.
    /// </summary>
    private static FrameworkElement Diagrams(ChordSheet sheet, double scale, Func<string, string> display, Action<string>? onClick)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 18 * scale, 0, 0) };
        foreach (var name in sheet.UsedChords())
        {
            FrameworkElement item;
            if (sheet.DiagramFor(name) is { } info)
                item = new ChordDiagramView(info, display(name), scale);
            else if (onClick != null)
            {
                var box = new StackPanel { Width = 84 * Math.Max(0.75, scale) };
                box.Children.Add(Text(display(name), Math.Max(13, 17 * scale), Res("SheetChord"), FontWeights.Bold));
                box.Children.Add(Text("? clic per scegliere il diagramma", 13, Res("SheetMuted"), FontWeights.Normal));
                item = box;
            }
            else continue;
            item.Margin = new Thickness(0, 0, 22 * scale, 10 * scale);
            item.Tag = name;
            if (onClick != null)
            {
                item.Cursor = System.Windows.Input.Cursors.Hand;
                item.ToolTip = $"{display(name)}: clic per scegliere il diagramma";
                item.MouseLeftButtonUp += (_, e) => { onClick(name); e.Handled = true; };
            }
            row.Children.Add(item);
        }
        return row;
    }

    private static TextBlock Text(string text, double size, Brush brush, FontWeight weight, double top = 0) => new()
    {
        Text = text, FontSize = size, Foreground = brush, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
        FontFamily = Font("UiFont"), Margin = new Thickness(0, top, 0, 0)
    };

    /// <summary>
    /// Una riga di testo: blocchi «accordo sopra, sillabe sotto». Dopo il primo, ogni parola è un
    /// blocco a sé, così la riga va a capo tra le parole anche con la carta stretta.
    /// </summary>
    private static FrameworkElement LyricRow(LyricLine line, double scale, Func<string, string> display)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 2 * scale, 0, 6 * scale) };
        var hasChords = line.Chords.Count > 0;
        if (line.Text.Trim().Length == 0 && !hasChords)
        {
            row.Children.Add(new Border { Height = LyricSize * scale * 0.8 }); // riga vuota tra due strofe
            return row;
        }
        var cuts = line.Chords.Select(c => c.Position).Prepend(0).Append(line.Text.Length).Distinct().OrderBy(p => p).ToList();
        for (var i = 0; i < cuts.Count - 1; i++)
        {
            var text = line.Text[cuts[i]..cuts[i + 1]];
            var chords = line.Chords.Where(c => c.Position == cuts[i]).Select(c => display(c.Name)).ToList();
            var words = SplitWords(text);
            if (words.Count == 0) words.Add("");
            for (var w = 0; w < words.Count; w++)
                row.Children.Add(Chunk(w == 0 ? string.Join(" ", chords) : "", words[w], hasChords, scale));
        }
        return row;
    }

    /// <summary>Parole con lo spazio che le segue; il primo pezzo resta attaccato all'accordo.</summary>
    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
            if (text[i] == ' ' && (i + 1 == text.Length || text[i + 1] != ' '))
            {
                words.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        if (start < text.Length) words.Add(text[start..]);
        return words;
    }

    private static FrameworkElement Chunk(string chord, string text, bool chordRow, double scale)
    {
        var block = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        if (chordRow)
            block.Children.Add(new TextBlock
            {
                // Spazio non separabile: la riga degli accordi ha la sua altezza anche senza accordo.
                Text = chord.Length > 0 ? chord : " ", FontSize = ChordSize * scale, FontWeight = FontWeights.Bold,
                Foreground = Res("SheetChord"), FontFamily = Font("UiFont"),
                Margin = new Thickness(0, 0, chord.Length > 0 ? 10 * scale : 0, 0)
            });
        // Lo spazio finale non conta nella larghezza di un TextBlock: diventa margine.
        var trailing = text.Length - text.TrimEnd(' ').Length;
        block.Children.Add(new TextBlock
        {
            Text = text.TrimEnd(' '), FontSize = LyricSize * scale, Foreground = Res("SheetInk"), FontFamily = Font("UiFont"),
            Margin = new Thickness(0, 0, trailing * LyricSize * scale * 0.28, 0), TextWrapping = TextWrapping.Wrap
        });
        return block;
    }

    /// <summary>Una fila del giro: «‖» iniziale e finale, «|» tra le battute, accordi in grassetto.</summary>
    private static FrameworkElement ProgressionRow(IReadOnlyList<IReadOnlyList<string>> bars, double scale, Func<string, string> display)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 4 * scale, 0, 8 * scale) };
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, MinWidth = 96 * scale };
            bar.Children.Add(Barline(scale, first: i == 0));
            var chords = bars[i].Count == 0 ? new[] { "–" } : bars[i].Select(display);
            foreach (var chord in chords)
                bar.Children.Add(new TextBlock
                {
                    Text = chord, FontSize = (ChordSize + 2) * scale, FontWeight = FontWeights.Bold, Foreground = Res("SheetChord"),
                    FontFamily = Font("UiFont"), Margin = new Thickness(10 * scale, 0, 8 * scale, 0), VerticalAlignment = VerticalAlignment.Center
                });
            row.Children.Add(bar);
        }
        row.Children.Add(Barline(scale, first: true));
        return row;
    }

    private static FrameworkElement Barline(double scale, bool first) => new Border
    {
        Width = first ? 3 * scale : 1.6 * scale, Height = 34 * scale, Background = Res("SheetMuted"),
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0)
    };
}
