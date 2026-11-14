using System.Windows;
using System.Windows.Controls;
using AlphaTab.Model;

namespace GuitarStudio.Views;

public sealed record TrackSettings(string Name, int Program, IList<double> Tuning, string TuningName, int Capo, bool IsBass);

public partial class TrackPropertiesWindow : Window
{
    private static readonly (int Program, string Name)[] Instruments =
    {
        (24, "Chitarra classica (nylon)"),
        (25, "Chitarra acustica (acciaio)"),
        (26, "Chitarra jazz"),
        (27, "Chitarra elettrica pulita"),
        (28, "Chitarra elettrica stoppata"),
        (29, "Chitarra overdrive"),
        (30, "Chitarra distorta"),
        (31, "Armonici di chitarra"),
        (32, "Basso acustico"),
        (33, "Basso elettrico (dita)"),
        (34, "Basso elettrico (plettro)"),
        (35, "Basso fretless"),
        (36, "Basso slap")
    };

    private readonly bool _isNew;
    private readonly IList<double>? _currentTuning;

    public TrackSettings? Result { get; private set; }

    /// <summary>Nuova traccia.</summary>
    public TrackPropertiesWindow()
    {
        InitializeComponent();
        _isNew = true;
        Header.Text = "Nuova traccia";
        NameBox.Text = "Chitarra 2";
        Init(30);
        TypeCombo.SelectedIndex = 0;
    }

    /// <summary>Modifica della traccia esistente (il numero di corde non si cambia).</summary>
    public TrackPropertiesWindow(Track track)
    {
        InitializeComponent();
        var staff = track.Staves[0];
        Header.Text = "Proprietà della traccia";
        NameBox.Text = track.Name;
        _currentTuning = staff.Tuning;
        Init((int)track.PlaybackInfo.Program);

        var strings = staff.Tuning.Count;
        var isBass = staff.Tuning.Max() < 55; // corda più acuta sotto il Sol3: è un basso
        TypeCombo.SelectedIndex = (strings, isBass) switch { (7, _) => 1, (4, _) => 2, (5, true) => 3, _ => 0 };
        TypeCombo.IsEnabled = false;
        CapoCombo.SelectedIndex = Math.Clamp((int)staff.Capo, 0, 12);
    }

    private void Init(int program)
    {
        foreach (var (p, name) in Instruments)
            InstrumentCombo.Items.Add(new ComboBoxItem { Content = name, Tag = p });
        var idx = Array.FindIndex(Instruments, i => i.Program == program);
        if (idx < 0)
        {
            InstrumentCombo.Items.Add(new ComboBoxItem { Content = $"Strumento MIDI {program}", Tag = program });
            idx = InstrumentCombo.Items.Count - 1;
        }
        InstrumentCombo.SelectedIndex = idx;

        for (var i = 0; i <= 12; i++) CapoCombo.Items.Add(i == 0 ? "nessuno" : i.ToString());
        CapoCombo.SelectedIndex = 0;
        Loaded += (_, _) => NameBox.Focus();
    }

    private (int Strings, bool Bass) SelectedType => ((ComboBoxItem)TypeCombo.SelectedItem).Tag switch
    {
        "7" => (7, false),
        "4b" => (4, true),
        "5b" => (5, true),
        _ => (6, false)
    };

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeCombo.SelectedItem == null) return;
        var (strings, bass) = SelectedType;

        TuningCombo.Items.Clear();
        var presets = Tuning.GetPresetsFor(strings)
            .Where(t => bass ? t.Name.StartsWith("Bass", StringComparison.OrdinalIgnoreCase) || strings == 5
                             : !t.Name.StartsWith("Bass", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (presets.Count == 0) presets.Add(Tuning.GetDefaultTuningFor(strings));

        var selected = 0;
        foreach (var t in presets)
        {
            TuningCombo.Items.Add(new ComboBoxItem { Content = Describe(t), Tag = t });
            if (_currentTuning != null && t.Tunings.SequenceEqual(_currentTuning)) selected = TuningCombo.Items.Count - 1;
        }
        if (_currentTuning != null && !presets.Any(t => t.Tunings.SequenceEqual(_currentTuning)))
        {
            var custom = new Tuning("Personalizzata", _currentTuning.ToList(), false);
            TuningCombo.Items.Insert(0, new ComboBoxItem { Content = Describe(custom), Tag = custom });
            selected = 0;
        }
        TuningCombo.SelectedIndex = selected;

        if (_isNew)
        {
            InstrumentCombo.SelectedIndex = Array.FindIndex(Instruments, i => i.Program == (bass ? 33 : 30));
            if (NameBox.Text is "Chitarra 2" or "Basso") NameBox.Text = bass ? "Basso" : "Chitarra 2";
        }
    }

    private static string Describe(Tuning t)
    {
        var notes = string.Join(" ", t.Tunings.Reverse().Select(v => Tuning.GetTextForTuning(v, false)));
        var name = t.Name
            .Replace("Guitar ", "").Replace("Bass ", "").Replace(" Tuning", "")
            .Replace("Standard", "Standard")
            .Replace("Tune down ½ step", "½ tono sotto")
            .Replace("Tune down 1 step", "1 tono sotto")
            .Replace("Tune down 2 step", "2 toni sotto")
            .Replace("Dropped", "Drop")
            .Replace("Double Drop", "Doppio drop")
            .Replace("variant", "variante");
        return $"{name}  —  {notes}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Dai un nome alla traccia.", "GuitarStudio");
            return;
        }
        var tuning = (Tuning)((ComboBoxItem)TuningCombo.SelectedItem).Tag;
        var program = (int)((ComboBoxItem)InstrumentCombo.SelectedItem).Tag;
        Result = new TrackSettings(name, program, tuning.Tunings.ToList(), tuning.Name, CapoCombo.SelectedIndex, SelectedType.Bass);
        DialogResult = true;
    }
}
