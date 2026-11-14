using System.Windows;
using System.Windows.Controls;
using AlphaTab.Model;
using GuitarStudio.Editing;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

public sealed record TrackSettings(string Name, int Program, IList<double> Tuning, string TuningName, int Capo, bool IsBass);

/// <summary>Finestra "Nuova traccia". Suoni e accordature vengono da <see cref="InstrumentCatalog"/>.</summary>
public partial class TrackPropertiesWindow : Window
{
    private const string DefaultGuitarName = "Chitarra 2";
    private const string DefaultBassName = "Basso";

    public TrackSettings? Result { get; private set; }

    public TrackPropertiesWindow()
    {
        InitializeComponent();
        WindowTheme.Apply(this);

        foreach (var (program, name) in InstrumentCatalog.Instruments)
            InstrumentCombo.Items.Add(new ComboBoxItem { Content = name, Tag = program });
        for (var i = 0; i <= 12; i++) CapoCombo.Items.Add(i == 0 ? "nessuno" : $"{i}° tasto");
        CapoCombo.SelectedIndex = 0;

        NameBox.Text = DefaultGuitarName;
        TypeCombo.SelectedIndex = 0;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
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
        if (TypeCombo.SelectedItem == null || TuningCombo == null) return;
        var (strings, bass) = SelectedType;

        TuningCombo.Items.Clear();
        foreach (var t in InstrumentCatalog.PresetsFor(strings, bass))
            TuningCombo.Items.Add(new ComboBoxItem { Content = InstrumentCatalog.Describe(t), Tag = t });
        TuningCombo.SelectedIndex = 0;

        // Suono e nome proposti seguono il tipo, finché l'utente non ha scritto un nome suo.
        var program = bass ? 33 : 30;
        InstrumentCombo.SelectedItem = InstrumentCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == program);
        if (NameBox.Text is DefaultGuitarName or DefaultBassName) NameBox.Text = bass ? DefaultBassName : DefaultGuitarName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            Dialogs.Info(this, "Dai un nome alla traccia.");
            NameBox.Focus();
            return;
        }
        if (TuningCombo.SelectedItem is not ComboBoxItem { Tag: Tuning tuning } ||
            InstrumentCombo.SelectedItem is not ComboBoxItem { Tag: int program }) return;
        Result = new TrackSettings(name, program, tuning.Tunings.ToList(), tuning.Name, CapoCombo.SelectedIndex, SelectedType.Bass);
        DialogResult = true;
    }
}
