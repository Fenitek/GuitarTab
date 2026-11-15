using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GuitarStudio.Views;

/// <summary>
/// Sezioni richiudibili della tavolozza. Ogni intestazione (SectionHeader) diventa un pulsante
/// che mostra o nasconde i controlli fino all'intestazione seguente; le sezioni chiuse sono
/// ricordate in AppSettings.CollapsedPaletteSections.
/// </summary>
public partial class PracticeView
{
    private readonly Dictionary<string, (ToggleButton Header, StackPanel Body)> _paletteSections = new();

    private void BuildPaletteSections()
    {
        // Le sezioni degli effetti sono generate dentro EffectsHost: si portano al livello della tavolozza.
        var flat = new List<UIElement>();
        foreach (var child in PaletteSections.Children.Cast<UIElement>().ToList())
        {
            if (ReferenceEquals(child, EffectsHost))
            {
                flat.AddRange(EffectsHost.Children.Cast<UIElement>().ToList());
                EffectsHost.Children.Clear();
            }
            else flat.Add(child);
        }
        PaletteSections.Children.Clear();

        var headerStyle = (Style)FindResource("SectionHeader");
        var toggleStyle = (Style)FindResource("SectionToggle");
        StackPanel? body = null;
        foreach (var element in flat)
        {
            if (element is TextBlock header && ReferenceEquals(header.Style, headerStyle))
            {
                var name = header.Text;
                var toggle = new ToggleButton { Style = toggleStyle, Content = name, Tag = name };
                body = new StackPanel();
                _paletteSections[name] = (toggle, body);
                toggle.Click += (_, _) => SetPaletteSectionOpen(name, toggle.IsChecked == true, save: true);
                PaletteSections.Children.Add(toggle);
                PaletteSections.Children.Add(body);
                SetPaletteSectionOpen(name, !AppSettings.Current.CollapsedPaletteSections.Contains(name), save: false);
            }
            else if (body != null && !ReferenceEquals(element, PaletteHelpButton))
                body.Children.Add(element);
            else
                PaletteSections.Children.Add(element);
        }
    }

    /// <summary>Apre o chiude una sezione della tavolozza; con save la scelta resta per i prossimi avvii.</summary>
    internal void SetPaletteSectionOpen(string name, bool open, bool save)
    {
        if (!_paletteSections.TryGetValue(name, out var section)) return;
        section.Header.IsChecked = open;
        section.Body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        var label = name[..1] + name[1..].ToLowerInvariant();
        AutomationProperties.SetName(section.Header, $"Sezione {label}, {(open ? "aperta" : "chiusa")}");
        section.Header.ToolTip = open ? $"Chiudi la sezione {label}" : $"Apri la sezione {label}";
        if (!save) return;
        var collapsed = AppSettings.Current.CollapsedPaletteSections;
        collapsed.RemoveAll(n => n == name);
        if (!open) collapsed.Add(name);
        AppSettings.Current.Save();
        Focus();
    }
}
