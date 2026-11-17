using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EagleEyes.Core;
namespace EagleEyes.App;

internal static class RegionSelection
{
    internal static Task<CaptureArea?> SelectAsync()
    {
        var completion = new TaskCompletionSource<CaptureArea?>(); var windows = new List<SelectionWindow>();
        void Finish(CaptureArea? area) { foreach (var w in windows) w.Close(); completion.TrySetResult(area); }
        foreach (var monitor in Native.Monitors()) { var w = new SelectionWindow(monitor, Finish); windows.Add(w); w.Show(); }
        if (windows.Count == 0) completion.TrySetException(new InvalidOperationException("Nenhum monitor disponível para seleção.")); else windows[0].Activate(); return completion.Task;
    }
    sealed class SelectionWindow : Window
    {
        readonly Native.Monitor monitor; readonly Action<CaptureArea?> finish; readonly Canvas canvas = new();
        readonly System.Windows.Shapes.Rectangle selection = new() { Stroke = Brushes.OrangeRed, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)) };
        Native.POINT start; bool dragging;
        internal SelectionWindow(Native.Monitor monitor, Action<CaptureArea?> finish)
        {
            this.monitor = monitor; this.finish = finish; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)); Cursor = Cursors.Cross;
            canvas.Children.Add(new TextBlock { Text = "Eagle Eyes · Arraste uma região neste monitor · Esc cancela", Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(12), FontSize = 16 }); canvas.Children.Add(selection); Content = canvas;
            SourceInitialized += (_, _) => Native.Place(this, monitor.Bounds);
            Loaded += (_, _) => Native.Place(this, monitor.Bounds);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) finish(null); };
            MouseLeftButtonDown += (_, _) => { Native.GetCursorPos(out start); dragging = true; CaptureMouse(); };
            MouseMove += (_, _) => { if (!dragging) return; var r = Selected(); var p = PointFromScreen(new Point(r.Left, r.Top)); var q = PointFromScreen(new Point(r.Right, r.Bottom)); Canvas.SetLeft(selection, p.X); Canvas.SetTop(selection, p.Y); selection.Width = Math.Max(0, q.X - p.X); selection.Height = Math.Max(0, q.Y - p.Y); };
            MouseLeftButtonUp += (_, _) => { if (!dragging) return; dragging = false; ReleaseMouseCapture(); var r = Selected(); if (r.Width < 16 || r.Height < 16) { MessageBox.Show("Selecione uma região de pelo menos 16 × 16 pixels.", "Eagle Eyes"); return; } finish(new CaptureArea(r.Left, r.Top, r.Width, r.Height)); };
        }
        Native.RECT Selected() { Native.GetCursorPos(out var p); int x = Math.Clamp(p.X, monitor.Bounds.Left, monitor.Bounds.Right), y = Math.Clamp(p.Y, monitor.Bounds.Top, monitor.Bounds.Bottom); int left = Math.Min(start.X, x), top = Math.Min(start.Y, y); return Native.Rectangle(left, top, Math.Abs(x - start.X), Math.Abs(y - start.Y)); }
    }
}
