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
        // Run continuations after the initiating mouse/key event has completely returned.
        var completion = new TaskCompletionSource<CaptureArea?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var windows = new List<SelectionWindow>(); bool finished = false;
        void Finish(CaptureArea? area)
        {
            if (finished) return;
            finished = true;
            foreach (var window in windows) { window.ReleaseMouseCapture(); window.Close(); }
            completion.TrySetResult(area);
        }
        foreach (var monitor in Native.Monitors()) { var window = new SelectionWindow(monitor, Finish); windows.Add(window); window.Show(); }
        if (windows.Count == 0) completion.TrySetException(new InvalidOperationException("Nenhum monitor disponível para seleção."));
        else windows[0].Activate();
        return completion.Task;
    }

    sealed class SelectionWindow : Window
    {
        readonly Native.Monitor monitor;
        readonly Action<CaptureArea?> finish;
        readonly Canvas canvas = new();
        readonly System.Windows.Shapes.Rectangle selection = new() { Stroke = Brushes.LimeGreen, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)) };
        Native.POINT start; bool dragging, spaceArmed, escapeArmed;
        internal SelectionWindow(Native.Monitor monitor, Action<CaptureArea?> finish)
        {
            this.monitor = monitor; this.finish = finish;
            Title = "Eagle Eyes · Selecionar área"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)); Cursor = Cursors.Cross;
            canvas.Children.Add(new TextBlock { Text = "Arraste para selecionar uma região · Espaço: monitor sob o ponteiro · Esc: cancelar", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(27, 27, 43)), Padding = new Thickness(14), FontSize = 16 });
            canvas.Children.Add(selection); Content = canvas;
            SourceInitialized += (_, _) => Native.Place(this, monitor.Bounds);
            Loaded += (_, _) => { Native.Place(this, monitor.Bounds); Native.TryExcludeFromCapture(this); };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Space) { e.Handled = true; if (!e.IsRepeat) spaceArmed = true; }
                if (e.Key == Key.Escape) { e.Handled = true; escapeArmed = true; }
            };
            PreviewKeyUp += (_, e) =>
            {
                if (e.Key == Key.Space)
                {
                    e.Handled = true;
                    if (!spaceArmed) return;
                    spaceArmed = false;
                    Native.GetCursorPos(out var point);
                    var underPointer = Native.Monitors().FirstOrDefault(m => point.X >= m.Bounds.Left && point.X < m.Bounds.Right && point.Y >= m.Bounds.Top && point.Y < m.Bounds.Bottom);
                    if (underPointer != null) finish(new CaptureArea(underPointer.Bounds.Left, underPointer.Bounds.Top, underPointer.Bounds.Width, underPointer.Bounds.Height));
                }
                if (e.Key == Key.Escape) { e.Handled = true; if (escapeArmed) { escapeArmed = false; finish(null); } }
            };
            MouseLeftButtonDown += (_, e) => { e.Handled = true; Native.GetCursorPos(out start); dragging = true; CaptureMouse(); };
            MouseMove += (_, _) =>
            {
                if (!dragging) return;
                var region = Selected(); var first = PointFromScreen(new Point(region.Left, region.Top)); var last = PointFromScreen(new Point(region.Right, region.Bottom));
                Canvas.SetLeft(selection, first.X); Canvas.SetTop(selection, first.Y); selection.Width = Math.Max(0, last.X - first.X); selection.Height = Math.Max(0, last.Y - first.Y);
            };
            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true; if (!dragging) return;
                dragging = false; ReleaseMouseCapture(); var region = Selected();
                if (region.Width < 16 || region.Height < 16) { MessageBox.Show("Selecione uma região de pelo menos 16 × 16 pixels.", "Eagle Eyes"); return; }
                finish(new CaptureArea(region.Left, region.Top, region.Width, region.Height));
            };
        }
        Native.RECT Selected()
        {
            Native.GetCursorPos(out var point);
            int x = Math.Clamp(point.X, monitor.Bounds.Left, monitor.Bounds.Right), y = Math.Clamp(point.Y, monitor.Bounds.Top, monitor.Bounds.Bottom);
            return Native.Rectangle(Math.Min(start.X, x), Math.Min(start.Y, y), Math.Abs(x - start.X), Math.Abs(y - start.Y));
        }
    }
}
