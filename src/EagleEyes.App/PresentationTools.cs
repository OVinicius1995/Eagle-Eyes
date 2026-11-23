using System.Drawing;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using EagleEyes.Core;
using Color = System.Windows.Media.Color;

namespace EagleEyes.App;

internal static class Screenshot
{
    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr handle);

    internal static BitmapSource Capture(CaptureArea area)
    {
        using var bitmap = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) throw new Win32Exception();
            try
            {
                var target = graphics.GetHdc();
                try
                {
                    // CAPTUREBLT includes layered windows such as the drawing surface.
                    if (!BitBlt(target, 0, 0, area.Width, area.Height, screen, area.X, area.Y, 0x40CC0020))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { graphics.ReleaseHdc(target); }
            }
            finally { ReleaseDC(IntPtr.Zero, screen); }
        }
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var opaque = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
            opaque.Freeze();
            return opaque;
        }
        finally { DeleteObject(handle); }
    }

    internal static void Save(BitmapSource image, string directory)
    {
        Directory.CreateDirectory(directory);
        var name = $"Print-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png";
        var path = Path.Combine(directory, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }
        catch { stream.Dispose(); File.Delete(path); throw; }
    }
}

internal sealed class AnnotationWindow : Window
{
    readonly InkCanvas surface = new();
    readonly CancellationTokenSource lifetime = new();

    internal AnnotationWindow(CaptureArea area, Color color)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        surface.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        surface.DefaultDrawingAttributes = new DrawingAttributes { Color = color, Width = 4, Height = 4, IgnorePressure = true };
        Content = surface;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000);
            Native.Place(this, Native.Rectangle(area.X, area.Y, area.Width, area.Height));
        };
        surface.StrokeCollected += async (_, e) =>
        {
            try { await Task.Delay(3000, lifetime.Token); surface.Strokes.Remove(e.Stroke); }
            catch (OperationCanceledException) { }
        };
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
    }

    internal void ExcludeToolbar(IntPtr toolbar)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !Native.GetWindowRect(handle, out var surfaceRect) || !Native.GetWindowRect(toolbar, out var bar)) return;
        var region = CreateRectRgn(0, 0, surfaceRect.Width, surfaceRect.Height);
        var cutout = CreateRectRgn(bar.Left - surfaceRect.Left, bar.Top - surfaceRect.Top,
            bar.Right - surfaceRect.Left, bar.Bottom - surfaceRect.Top);
        try
        {
            if (region != IntPtr.Zero && cutout != IntPtr.Zero && CombineRgn(region, region, cutout, 4) != 0 &&
                SetWindowRgn(handle, region, true) != 0)
                region = IntPtr.Zero; // Windows owns the applied region.
        }
        finally
        {
            if (region != IntPtr.Zero) DeleteObject(region);
            if (cutout != IntPtr.Zero) DeleteObject(cutout);
        }
    }

    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dest, IntPtr first, IntPtr second, int mode);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    internal void SetColor(Color color) => surface.DefaultDrawingAttributes.Color = color;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}

