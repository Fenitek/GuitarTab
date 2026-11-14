using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuitarStudio.Ui;

public enum DialogKind { Info, Question, Warning, Error }

/// <summary>Finestre di messaggio nel tema dell'app (al posto di MessageBox, che è chiara).</summary>
public static class Dialogs
{
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

    private static MessageBoxResult Show(Window? owner, string title, string message, DialogKind kind,
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
        body.Children.Add(new TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14.5, LineHeight = 21,
            Foreground = Res("TextMain"), VerticalAlignment = VerticalAlignment.Center
        });

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
            b.Click += (_, _) => { result = value; window.Close(); };
            row.Children.Add(b);
        }

        var root = new StackPanel();
        root.Children.Add(body);
        root.Children.Add(row);
        window.Content = root;
        window.ShowDialog();
        return result;
    }
}
