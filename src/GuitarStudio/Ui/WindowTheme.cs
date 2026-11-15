using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GuitarStudio.Ui;

/// <summary>Applica il tema a una finestra: caratteri di base e barra del titolo scura.</summary>
public static class WindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    public static void Apply(Window window)
    {
        window.FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
        window.FontSize = 13.5;
        window.UseLayoutRounding = true;
        window.Foreground = (Brush)Application.Current.FindResource("TextMain");
        if (Environment.GetEnvironmentVariable("GS_SELFTEST") == "1") window.ShowActivated = false;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) ApplyTitleBar(window);
        else window.SourceInitialized += (_, _) => ApplyTitleBar(window);
    }

    private static void ApplyTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            // Le sole finestre dell'autotest non devono sottrarre il focus all'utente,
            // neppure quando WPF chiude una finestra figlia o seleziona un divisore.
            if (Environment.GetEnvironmentVariable("GS_SELFTEST") == "1")
                SetWindowLongPtr(hwnd, -20, new nint(GetWindowLongPtr(hwnd, -20).ToInt64() | (1L << 27)));
            var on = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
            // Windows 11: gli stessi colori del tema, convertiti nel formato COLORREF (BGR).
            var caption = ColorRef("BgPanel");
            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
            var border = ColorRef("Line");
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
        }
        catch
        {
            // Su sistemi che non lo supportano resta la barra standard.
        }
    }

    private static int ColorRef(string resource)
    {
        var color = ((SolidColorBrush)Application.Current.FindResource(resource)).Color;
        return color.R | (color.G << 8) | (color.B << 16);
    }
}
