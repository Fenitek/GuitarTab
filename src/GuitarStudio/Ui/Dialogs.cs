using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GuitarStudio.Editing;

namespace GuitarStudio.Ui;

public enum DialogKind { Info, Question, Warning, Error }

/// <summary>Finestre di messaggio nel tema dell'app (al posto di MessageBox, che è chiara).</summary>
public static class Dialogs
{
    internal static Func<Window, MessageBoxResult>? SelfTestPresenter;
    public static void Info(Window? owner, string message, string? title = null) =>
        Show(owner, title ?? "GuitarStudio", message, DialogKind.Info, ("OK", MessageBoxResult.OK, true));

    public static void Warning(Window? owner, string message, string? title = null) =>
        Show(owner, title ?? "Attenzione", message, DialogKind.Warning, ("OK", MessageBoxResult.OK, true));

    public static void Error(Window? owner, string message, string? title = null) =>
        Show(owner, title ?? "Errore", message, DialogKind.Error, ("OK", MessageBoxResult.OK, true));

    /// <summary>Domanda con due risposte. Restituisce true se l'utente conferma.</summary>
    public static bool Confirm(Window? owner, string message, string title, string yes, string no = "Annulla") =>
        Show(owner, title, message, DialogKind.Question,
            (no, MessageBoxResult.No, false), (yes, MessageBoxResult.Yes, true)) == MessageBoxResult.Yes;

    /// <summary>Salvare prima di chiudere? Yes = salva, No = non salvare, Cancel = resta.</summary>
    public static MessageBoxResult SaveChanges(Window? owner, string message) =>
        Show(owner, "Modifiche non salvate", message, DialogKind.Question,
            ("Annulla", MessageBoxResult.Cancel, false),
            ("Non salvare", MessageBoxResult.No, false),
            ("Salva", MessageBoxResult.Yes, true));

    public static MessageBoxResult RecoverAutosave(Window? owner, string message) =>
        Show(owner, "Recupera tablatura", message, DialogKind.Question,
            ("Più tardi", MessageBoxResult.Cancel, false),
            ("Elimina copia", MessageBoxResult.No, false),
            ("Recupera", MessageBoxResult.Yes, true));

    /// <summary>
    /// Nome e diagramma dell'accordo sopra il tempo. Yes = applica (Chord valorizzato),
    /// No = togli l'accordo esistente, Cancel = nessuna modifica.
    /// </summary>
    public static (MessageBoxResult Result, ChordInfo? Chord) Chord(Window? owner, ChordInfo initial, bool canRemove)
    {
        var strings = initial.FretsLowToHigh.Count;
        var name = new TextBox { Name = "ChordNameBox", Text = initial.Name, MaxLength = 24 };
        AutomationProperties.SetName(name, "Nome dell'accordo");
        name.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
        var diagram = new CheckBox { Name = "ChordDiagramCheck", Content = "Mostra il diagramma", IsChecked = initial.ShowDiagram, Margin = new Thickness(0, 14, 0, 0) };
        var frets = new TextBox { Name = "ChordFretsBox", Text = TabEditor.FormatFrets(initial.FretsLowToHigh), FontFamily = (FontFamily)Application.Current.FindResource("MonoFont") };
        AutomationProperties.SetName(frets, "Tasti del diagramma dalla corda più grave");
        var fields = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        fields.Children.Add(Label("Nome (es. Do, Am7, G/B)"));
        fields.Children.Add(name);
        fields.Children.Add(diagram);
        fields.Children.Add(Label($"Diagramma: {strings} tasti dalla corda più grave alla più acuta, x = non suonata"));
        fields.Children.Add(frets);

        ChordInfo? result = null;
        var choice = Show(owner, "Accordo", "Il nome compare sopra il tempo sotto il cursore; il diagramma, se attivo, in cima alla partitura.",
            DialogKind.Question, fields, value =>
            {
                if (value != MessageBoxResult.Yes) return null;
                if (string.IsNullOrWhiteSpace(name.Text)) return "Scrivi il nome dell'accordo.";
                if (TabEditor.ParseFrets(frets.Text, strings) is not { } parsed)
                    return $"Il diagramma vuole {strings} valori separati da spazi: un tasto da 0 a 24 oppure x.";
                result = new ChordInfo(name.Text.Trim(), diagram.IsChecked == true, parsed);
                return null;
            },
            [("Annulla", MessageBoxResult.Cancel, false),
             .. canRemove ? [("Togli accordo", MessageBoxResult.No, false)] : Array.Empty<(string, MessageBoxResult, bool)>(),
             ("Applica", MessageBoxResult.Yes, true)]);
        return (choice, choice == MessageBoxResult.Yes ? result : null);
    }

    /// <summary>Testo libero sopra il tempo. Yes = applica (vuoto = togli), No = togli, Cancel = niente.</summary>
    public static (MessageBoxResult Result, string Text) BeatText(Window? owner, string initial, bool canRemove)
    {
        var box = new TextBox { Name = "BeatTextBox", Text = initial, MaxLength = 60 };
        AutomationProperties.SetName(box, "Testo sopra il tempo");
        box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        var fields = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        fields.Children.Add(Label("Testo (es. rallentando, Strofa 2, a tempo)"));
        fields.Children.Add(box);
        var choice = Show(owner, "Testo sopra il tempo", "Il testo compare sopra il tempo sotto il cursore e si salva nel file.",
            DialogKind.Question, fields, null,
            [("Annulla", MessageBoxResult.Cancel, false),
             .. canRemove ? [("Togli testo", MessageBoxResult.No, false)] : Array.Empty<(string, MessageBoxResult, bool)>(),
             ("Applica", MessageBoxResult.Yes, true)]);
        return (choice, box.Text.Trim());
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text, Style = (Style)Application.Current.FindResource("FieldLabel"), TextWrapping = TextWrapping.Wrap
    };

    private static MessageBoxResult Show(Window? owner, string title, string message, DialogKind kind,
        params (string Text, MessageBoxResult Result, bool Primary)[] buttons) =>
        Show(owner, title, message, kind, null, null, buttons);

    /// <param name="fields">Campi sotto il messaggio (dialoghi di inserimento).</param>
    /// <param name="accept">Controlla i campi prima di chiudere: restituisce un errore da mostrare, o null.</param>
    private static MessageBoxResult Show(Window? owner, string title, string message, DialogKind kind,
        UIElement? fields, Func<MessageBoxResult, string?>? accept,
        params (string Text, MessageBoxResult Result, bool Primary)[] buttons)
    {
        var app = Application.Current;
        Brush Res(string key) => (Brush)app.FindResource(key);

        owner ??= app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? app.MainWindow;
        if (owner is { IsLoaded: false }) owner = null;

        var result = buttons[0].Result; // chiudere la finestra equivale alla prima risposta (la più prudente)
        var window = new Window
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = owner == null,
            Background = Res("BgPanel"),
            MinWidth = 380,
            MaxWidth = 560
        };
        WindowTheme.Apply(window);

        var (glyph, color) = kind switch
        {
            DialogKind.Question => ("", Res("Accent")),
            DialogKind.Warning => ("", Res("Accent")),
            DialogKind.Error => ("", Res("Danger")),
            _ => ("", Res("TextDim"))
        };

        var body = new DockPanel { Margin = new Thickness(24, 22, 24, 6) };
        var icon = new TextBlock
        {
            Text = glyph, FontFamily = (FontFamily)app.FindResource("IconFont"), FontSize = 30,
            Foreground = color, Margin = new Thickness(0, 2, 18, 0), VerticalAlignment = VerticalAlignment.Top
        };
        DockPanel.SetDock(icon, Dock.Left);
        body.Children.Add(icon);
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14.5, LineHeight = 21,
            Foreground = Res("TextMain")
        });
        if (fields != null) content.Children.Add(fields);
        var error = new TextBlock
        {
            Name = "DialogError", Foreground = Res("Danger"), FontSize = 13.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed
        };
        content.Children.Add(error);
        body.Children.Add(content);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(24, 18, 16, 20)
        };
        foreach (var (text, value, primary) in buttons)
        {
            var b = new Button
            {
                Content = text, MinWidth = 104,
                Style = (Style)app.FindResource(primary ? "PrimaryButton" : "ToolButton"),
                IsDefault = primary,
                IsCancel = buttons.Length > 1 && value == buttons[0].Result
            };
            b.Click += (_, _) =>
            {
                if (value != buttons[0].Result && accept?.Invoke(value) is { } problem)
                {
                    error.Text = problem;
                    error.Visibility = Visibility.Visible;
                    return;
                }
                result = value;
                window.Close();
            };
            row.Children.Add(b);
        }

        var root = new StackPanel();
        root.Children.Add(body);
        root.Children.Add(row);
        window.Content = root;
        if (Environment.GetEnvironmentVariable("GS_SELFTEST") == "1" && SelfTestPresenter is { } present)
        {
            result = present(window);
            // Nell'autotest i campi compilati dal presentatore passano gli stessi controlli dei clic.
            if (result != buttons[0].Result && accept?.Invoke(result) is { } problem)
                throw new InvalidOperationException("Dialogo «" + title + "»: " + problem);
        }
        else
            window.ShowDialog();
        return result;
    }
}
