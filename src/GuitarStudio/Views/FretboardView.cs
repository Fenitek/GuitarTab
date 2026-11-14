using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GuitarStudio.Views;

/// <summary>
/// Manico disegnato: mostra le note del tempo corrente (o di quello in riproduzione)
/// e permette di scrivere una nota cliccando su corda e tasto.
/// </summary>
public sealed class FretboardView : FrameworkElement
{
    public const int Frets = 24;

    private Brush Res(string key) => (Brush)FindResource(key);
    private Brush Wood => Res("FretboardWood");
    private Pen FretPen => (Pen)FindResource("FretboardFretPen");
    private Pen NutPen => (Pen)FindResource("FretboardNutPen");
    private Brush Inlay => Res("FretboardInlay");
    private Brush NoteFill => Res("Accent");
    private Brush PlayFill => Res("PlaybackNote");
    private Brush CursorBand => Res("FretboardCursorBand");
    private Brush HoverFill => Res("FretboardHover");
    private Brush LabelBrush => Res("FretboardLabel");
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly int[] InlayFrets = { 3, 5, 7, 9, 15, 17, 19, 21 };

    private const double LeftLabels = 34;   // nomi delle corde
    private const double OpenArea = 30;     // zona delle corde a vuoto prima del capotasto
    private const double BottomLabels = 20; // spazio per i numeri dei tasti a 13 px

    private IReadOnlyList<(int Line, int Fret)> _notes = Array.Empty<(int, int)>();
    private bool _isPlaying;
    private (int Line, int Fret)? _hover;

    public int StringCount { get; private set; } = 6;
    public int CursorLine { get; private set; } = 1;
    public IReadOnlyList<string> StringNames { get; private set; } = new[] { "E", "B", "G", "D", "A", "E" };
    public int Capo { get; private set; }

    /// <summary>Clic su (corda, tasto). La corda è numerata dall'alto (1 = la più acuta).</summary>
    public event Action<int, int>? FretClicked;

    public FretboardView()
    {
        Height = 150;
        Cursor = Cursors.Hand;
        ToolTip = "Clicca su un tasto per scrivere la nota nel tempo selezionato; clicca di nuovo per toglierla";
    }

    public void Show(int stringCount, IReadOnlyList<string> names, int capo, int cursorLine,
        IReadOnlyList<(int Line, int Fret)> notes, bool isPlaying)
    {
        StringCount = Math.Max(1, stringCount);
        StringNames = names;
        Capo = capo;
        CursorLine = cursorLine;
        _notes = notes;
        _isPlaying = isPlaying;
        InvalidateVisual();
    }

    // Posizione del tasto come su un manico vero (i tasti si stringono verso il corpo).
    private double FretX(double fret)
    {
        var start = LeftLabels + OpenArea;
        var width = Math.Max(100, ActualWidth - start - 8);
        var full = 1 - Math.Pow(2, -Frets / 12.0);
        return start + width * (1 - Math.Pow(2, -fret / 12.0)) / full;
    }

    private double StringY(int line)
    {
        var top = 12.0;
        var h = Math.Max(40, ActualHeight - BottomLabels - top - 10);
        return StringCount == 1 ? top + h / 2 : top + h * (line - 1) / (StringCount - 1);
    }

    private double NoteX(int fret) => fret == 0 ? LeftLabels + OpenArea / 2 : (FretX(fret - 1) + FretX(fret)) / 2;

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var top = StringY(1) - 9;
        var bottom = StringY(StringCount) + 9;
        dc.DrawRectangle(Res("TransparentFill"), null, new Rect(0, 0, w, ActualHeight));
        dc.DrawRoundedRectangle(Wood, null, new Rect(LeftLabels + OpenArea, top, FretX(Frets) - LeftLabels - OpenArea, bottom - top), 3, 3);

        // Riga della corda selezionata.
        var cy = StringY(CursorLine);
        dc.DrawRectangle(CursorBand, null, new Rect(LeftLabels - 4, cy - 9, w - LeftLabels, 18));

        // Segni di posizione.
        var midY = (top + bottom) / 2;
        foreach (var f in InlayFrets)
            dc.DrawEllipse(Inlay, null, new Point(NoteX(f), midY), 5, 5);
        dc.DrawEllipse(Inlay, null, new Point(NoteX(12), midY - (bottom - top) / 4), 5, 5);
        dc.DrawEllipse(Inlay, null, new Point(NoteX(12), midY + (bottom - top) / 4), 5, 5);
        dc.DrawEllipse(Inlay, null, new Point(NoteX(24), midY - (bottom - top) / 4), 5, 5);
        dc.DrawEllipse(Inlay, null, new Point(NoteX(24), midY + (bottom - top) / 4), 5, 5);

        // Tasti e capotasto.
        dc.DrawLine(NutPen, new Point(FretX(0), top), new Point(FretX(0), bottom));
        for (var f = 1; f <= Frets; f++)
        {
            dc.DrawLine(FretPen, new Point(FretX(f), top), new Point(FretX(f), bottom));
            if (f is 3 or 5 or 7 or 9 or 12 or 15 or 17 or 19 or 21 or 24)
                DrawText(dc, f.ToString(), NoteX(f), ActualHeight - BottomLabels / 2 - 1, 13, LabelBrush);
        }
        if (Capo > 0)
        {
            var x = (FretX(Capo - 1) + FretX(Capo)) / 2;
            dc.DrawRoundedRectangle(Res("FretboardCapo"), null,
                new Rect(x - 5, top - 4, 10, bottom - top + 8), 4, 4);
        }

        // Corde (le gravi più spesse) e nomi.
        for (var line = 1; line <= StringCount; line++)
        {
            var y = StringY(line);
            var thickness = 0.8 + 1.8 * (line - 1) / Math.Max(1, StringCount - 1);
            dc.DrawLine(new Pen(Res("FretboardString"), thickness),
                new Point(LeftLabels + 6, y), new Point(FretX(Frets), y));
            var name = line - 1 < StringNames.Count ? StringNames[line - 1] : "";
            DrawText(dc, name, LeftLabels / 2, y, 13, line == CursorLine ? NoteFill : LabelBrush);
        }

        // Nota sotto il mouse.
        if (_hover is { } h && !_notes.Contains(h))
            dc.DrawEllipse(HoverFill, null, new Point(NoteX(h.Fret), StringY(h.Line)), 9, 9);

        // Note del tempo.
        var fill = _isPlaying ? PlayFill : NoteFill;
        foreach (var (line, fret) in _notes)
        {
            if (line < 1 || line > StringCount || fret < 0 || fret > Frets) continue;
            var p = new Point(NoteX(fret), StringY(line));
            dc.DrawEllipse(fill, new Pen(Res("FretboardNoteInk"), 1), p, 10, 10);
            DrawText(dc, fret.ToString(), p.X, p.Y, 13, Res("FretboardNoteInk"), bold: true);
        }
    }

    private void DrawText(DrawingContext dc, string text, double cx, double cy, double size, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            bold ? new Typeface(Face.FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal) : Face,
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    private (int Line, int Fret)? HitTest(Point p)
    {
        if (p.X < LeftLabels) return null;
        var line = 1;
        var best = double.MaxValue;
        for (var l = 1; l <= StringCount; l++)
        {
            var d = Math.Abs(StringY(l) - p.Y);
            if (d < best) { best = d; line = l; }
        }
        if (best > 14) return null;

        int fret;
        if (p.X < FretX(0)) fret = 0;
        else
        {
            fret = Frets;
            for (var f = 1; f <= Frets; f++)
                if (p.X < FretX(f)) { fret = f; break; }
        }
        return (line, fret);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = HitTest(e.GetPosition(this));
        if (h != _hover)
        {
            _hover = h;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (HitTest(e.GetPosition(this)) is { } h)
        {
            FretClicked?.Invoke(h.Line, h.Fret);
            e.Handled = true;
        }
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
        new PointHitTestResult(this, hitTestParameters.HitPoint);
}
