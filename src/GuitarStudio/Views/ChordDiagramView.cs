using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GuitarStudio.Editing;

namespace GuitarStudio.Views;

/// <summary>
/// Diagramma d'accordo disegnato sulla carta del foglio: nome, corde mute (×) e a vuoto (○),
/// cinque tasti, pallini e numero del primo tasto quando la forma è in alto sul manico.
/// Riceve la stessa <see cref="ChordInfo"/> degli accordi della tablatura.
/// </summary>
public sealed class ChordDiagramView : FrameworkElement
{
    private const int Frets = 5;
    private readonly ChordInfo _info;
    private readonly string _label;
    private readonly double _scale;

    public ChordDiagramView(ChordInfo info, string label, double scale)
    {
        _info = info;
        _label = label;
        _scale = Math.Max(0.75, scale);
        ToolTip = $"{label}: {TabEditor.FormatFrets(info.FretsLowToHigh)} (dalla corda più grave)";
    }

    public string ChordName => _info.Name;

    // Le corde stanno almeno alla larghezza di un simbolo ○/× da 13 px: nell'anteprima non si toccano.
    private double Spacing => Math.Max(15 * _scale, SmallSize * 1.08);
    private double FretHeight => 15 * _scale;
    private double LabelSize => Math.Max(13, 17 * _scale);
    private double SmallSize => Math.Max(13, 12 * _scale);
    private double Left => SmallSize * 1.6;
    private double Top => LabelSize * 1.5 + SmallSize * 1.2;

    private int FirstFret
    {
        get
        {
            var played = _info.FretsLowToHigh.Where(f => f is > 0).Select(f => f!.Value).ToList();
            return played.Count > 0 && played.Max() > Frets ? played.Min() : 1;
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Left + Spacing * (Strings - 1) + Spacing * 1.2, Top + FretHeight * Frets + 4 * _scale);

    private int Strings => Math.Max(2, _info.FretsLowToHigh.Count);

    private FormattedText Text(string text, double size, Brush brush, FontWeight weight) => new(
        text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface((FontFamily)FindResource("UiFont"), FontStyles.Normal, weight, FontStretches.Normal),
        size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        var ink = (Brush)FindResource("SheetInk");
        var muted = (Brush)FindResource("SheetMuted");
        var chord = (Brush)FindResource("SheetChord");
        var gridWidth = Spacing * (Strings - 1);

        var name = Text(_label, LabelSize, chord, FontWeights.Bold);
        dc.DrawText(name, new Point(Left + gridWidth / 2 - name.Width / 2, 0));

        var first = FirstFret;
        var line = new Pen(muted, Math.Max(1, 1.1 * _scale));
        var nut = new Pen(ink, Math.Max(2, 3.2 * _scale));
        for (var s = 0; s < Strings; s++)
            dc.DrawLine(line, new Point(Left + s * Spacing, Top), new Point(Left + s * Spacing, Top + FretHeight * Frets));
        for (var f = 0; f <= Frets; f++)
            dc.DrawLine(f == 0 && first == 1 ? nut : line, new Point(Left, Top + f * FretHeight), new Point(Left + gridWidth, Top + f * FretHeight));
        if (first > 1)
        {
            var number = Text(first.ToString(CultureInfo.InvariantCulture), SmallSize, muted, FontWeights.SemiBold);
            dc.DrawText(number, new Point(Left - number.Width - 4 * _scale, Top + FretHeight / 2 - number.Height / 2));
        }

        for (var s = 0; s < Strings; s++)
        {
            var x = Left + s * Spacing;
            var fret = _info.FretsLowToHigh[s];
            if (fret is not { } value || value == 0)
            {
                var mark = Text(fret == null ? "×" : "○", SmallSize, ink, FontWeights.Normal);
                dc.DrawText(mark, new Point(x - mark.Width / 2, Top - mark.Height - 1 * _scale));
                continue;
            }
            var row = value - first;
            if (row < 0 || row >= Frets) continue;
            dc.DrawEllipse(ink, null, new Point(x, Top + (row + 0.5) * FretHeight), Spacing * 0.36, Spacing * 0.36);
        }
    }
}
