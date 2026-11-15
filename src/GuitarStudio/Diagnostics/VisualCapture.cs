using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GuitarStudio.Diagnostics;

/// <summary>Foto delle sole finestre dell'autotest tramite PrintWindow, senza input o cattura del desktop.</summary>
internal sealed class VisualCapture(string directory)
{
    private readonly List<object> _shots = new();
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint hwnd, nint dc, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    internal static async Task Paint()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(240);
    }

    // Il presentatore sincrono dei dialoghi lascia elaborare layout/paint, senza ShowDialog/Activate.
    internal static void PaintSynchronously()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    internal static void Size(Window window, bool minimum)
    {
        window.WindowState = WindowState.Normal;
        var dpi = VisualTreeHelper.GetDpi(window);
        window.Width = minimum ? window.MinWidth : 1920 / dpi.DpiScaleX;
        window.Height = minimum ? window.MinHeight : 1080 / dpi.DpiScaleY;
        window.Left = 40; window.Top = 40;
        window.UpdateLayout();
    }

    internal static void SizeAtScale(Window window, int width, int height, double scale)
    {
        window.WindowState = WindowState.Normal;
        var dpi = VisualTreeHelper.GetDpi(window);
        // Scala solo i contenuti della finestra di prova: nessuna preferenza dello schermo viene modificata.
        if (window.Content is FrameworkElement content)
            content.LayoutTransform = new ScaleTransform(scale / dpi.DpiScaleX, scale / dpi.DpiScaleY);
        window.MinWidth = Math.Min(window.MinWidth, width / dpi.DpiScaleX);
        window.MinHeight = Math.Min(window.MinHeight, height / dpi.DpiScaleY);
        window.Width = width / dpi.DpiScaleX;
        window.Height = height / dpi.DpiScaleY;
        window.Left = 24; window.Top = 24;
        window.UpdateLayout();
    }

    internal static void ScalePopup(FrameworkElement root, double scale)
    {
        if (PresentationSource.FromVisual(root) is not HwndSource source) return;
        var current = root.TransformToAncestor(source.RootVisual).TransformBounds(new Rect(0, 0, 1, 1));
        var dpi = VisualTreeHelper.GetDpi(root);
        var previous = root.LayoutTransform is ScaleTransform transform ? transform.ScaleX : 1;
        root.LayoutTransform = new ScaleTransform(previous * scale / (dpi.DpiScaleX * current.Width),
            previous * scale / (dpi.DpiScaleY * current.Height));
        root.UpdateLayout();
    }

    internal void Save(Window frame, string name, params Visual[] overlays)
    {
        Directory.CreateDirectory(directory);
        var handle = new WindowInteropHelper(frame).Handle;
        if (Environment.GetEnvironmentVariable("GS_SELFTEST_LAYOUT") == "1" && GetForegroundWindow() == handle)
            throw new InvalidOperationException("La finestra di prova non deve prendere il focus dell'utente.");
        var bitmap = Capture(handle, out var bounds);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            for (var i = 0; i < overlays.Length; i++)
            {
                if (PresentationSource.FromVisual(overlays[i]) is not HwndSource source || source.Handle == handle) continue;
                var overlay = Capture(source.Handle, out var rect);
                Write(overlay, Path.Combine(directory, $"{name}-popup-{i + 1}.png"));
                dc.DrawImage(overlay, new Rect(rect.Left - bounds.Left, rect.Top - bounds.Top, overlay.PixelWidth, overlay.PixelHeight));
            }
        }
        var composite = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        composite.Render(drawing);
        Write(composite, Path.Combine(directory, name + ".png"));
        var roots = new DependencyObject[] { frame }.Concat(overlays);
        var text = roots.SelectMany(Descendants<TextBlock>).Where(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Text));
        _shots.Add(new
        {
            File = name + ".png", Width = bitmap.PixelWidth, Height = bitmap.PixelHeight,
            LogicalWidth = frame.ActualWidth, LogicalHeight = frame.ActualHeight,
            Dpi = VisualTreeHelper.GetDpi(frame).PixelsPerInchX,
            EffectiveDpi = VisualTreeHelper.GetDpi(frame).PixelsPerInchX *
                ((frame.Content as FrameworkElement)?.LayoutTransform is ScaleTransform scale ? scale.ScaleX : 1),
            SmallText = text.Where(t => t.FontSize < 13).Select(t => new { t.Text, t.FontSize }).ToArray()
        });
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(_shots, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static BitmapSource Capture(nint hwnd, out NativeRect rect)
    {
        if (!GetWindowRect(hwnd, out rect)) throw new InvalidOperationException("GetWindowRect non riuscito.");
        var dc = GetDC(hwnd);
        var memory = CreateCompatibleDC(dc);
        var bitmap = CreateCompatibleBitmap(dc, rect.Right - rect.Left, rect.Bottom - rect.Top);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(hwnd, memory, 2)) throw new InvalidOperationException("PrintWindow non riuscito.");
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            SelectObject(memory, previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(hwnd, dc);
        }
    }

    private static void Write(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
