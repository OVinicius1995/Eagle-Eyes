using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EagleEyes.Core;
namespace EagleEyes.App;

internal enum PreparationAction { Start, Reselect, Cancel }
internal sealed class PreparationWindow : Window
{
    readonly CaptureArea area;
    readonly Window highlight;
    readonly StackPanel actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    TaskCompletionSource<PreparationAction>? pending;
    bool closed, positioned;
    internal PreparationWindow(CaptureArea area)
    {
        this.area = area;
        Title = "Eagle Eyes · Preparar gravação"; Width = 478; Height = 92; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        var panel = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
        panel.Children.Add(new TextBlock { Text = "Tudo pronto! Clique em Gravar para começar.", ToolTip = $"Área selecionada: {area.Width} × {area.Height} pixels", Foreground = Brushes.White, FontSize = 13, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Center });
        actions.Children.Add(Button("Gravar", PreparationAction.Start, Color.FromRgb(46, 160, 95)));
        actions.Children.Add(Button("Selecionar novamente", PreparationAction.Reselect, Color.FromRgb(62, 64, 82)));
        actions.Children.Add(Button("Cancelar", PreparationAction.Cancel, Color.FromRgb(62, 64, 82)));
        panel.Children.Add(actions); Content = new Border { Background = new SolidColorBrush(Color.FromRgb(27, 27, 43)), CornerRadius = new CornerRadius(12), Child = panel };
        highlight = new Window { Title = "Eagle Eyes · Área preparada", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent, IsHitTestVisible = false, Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(60, 210, 125)), BorderThickness = new Thickness(3), Background = Brushes.Transparent } };
        highlight.SourceInitialized += (_, _) => { Native.MakeClickThrough(highlight); Native.TryExcludeFromCapture(highlight); };
        highlight.Loaded += (_, _) => PositionHighlight();
        Loaded += (_, _) => { Native.TryExcludeFromCapture(this); if (!positioned) PositionBar(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Respond(PreparationAction.Cancel); } };
        WindowDragBehavior.Attach(this, () => positioned = true);
        Closed += (_, _) => { closed = true; highlight.Close(); pending?.TrySetResult(PreparationAction.Cancel); pending = null; };
    }
    Button Button(string title, PreparationAction action, Color color)
    {
        var button = new Button { Content = title, Background = new SolidColorBrush(color), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(4, 0, 4, 0), Cursor = Cursors.Hand };
        AutomationProperties.SetName(button, title); button.Click += (_, _) => Respond(action); return button;
    }
    internal Task<PreparationAction> NextActionAsync()
    {
        if (closed) return Task.FromResult(PreparationAction.Cancel);
        if (pending != null) return pending.Task;
        pending = new TaskCompletionSource<PreparationAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = pending.Task; actions.IsEnabled = true; highlight.Show(); PositionHighlight(); Show(); if (!positioned) PositionBar(); Native.ClampToWorkArea(this); Activate();
        return result;
    }
    void Respond(PreparationAction action)
    {
        var current = pending; if (current == null) return;
        pending = null; actions.IsEnabled = false; current.TrySetResult(action);
    }
    internal void HideAll() { Hide(); highlight.Hide(); }
    void PositionHighlight() => Native.Place(highlight, Native.Rectangle(area.X - 3, area.Y - 3, area.Width + 6, area.Height + 6));
    void PositionBar()
    {
        var monitors = Native.Monitors(); if (monitors.Count == 0) return;
        long centerX = (long)area.X + area.Width / 2, centerY = (long)area.Y + area.Height / 2;
        var monitor = monitors.FirstOrDefault(m => centerX >= m.Bounds.Left && centerX < m.Bounds.Right && centerY >= m.Bounds.Top && centerY < m.Bounds.Bottom) ?? monitors[0];
        var handle = new WindowInteropHelper(this).EnsureHandle();
        // Move to the target monitor first so its actual per-monitor DPI determines the panel size.
        Native.Place(this, Native.Rectangle(monitor.Work.Left, monitor.Work.Top, (int)Width, (int)Height));
        double scale = Math.Max(1, Native.GetDpiForWindow(handle) / 96d);
        int width = (int)Math.Ceiling(Width * scale), height = (int)Math.Ceiling(Height * scale), gap = (int)(12 * scale);
        var work = monitor.Work; int x = area.X + (area.Width - width) / 2, y;
        if (area.Y - height - gap >= work.Top) y = area.Y - height - gap;
        else if (area.Y + area.Height + gap + height <= work.Bottom) y = area.Y + area.Height + gap;
        else if (area.X - width - gap >= work.Left) { x = area.X - width - gap; y = area.Y; }
        else if (area.X + area.Width + gap + width <= work.Right) { x = area.X + area.Width + gap; y = area.Y; }
        else { x = work.Left + (work.Width - width) / 2; y = work.Top + gap; }
        Native.Place(this, Native.Rectangle(x, y, width, height)); Native.ClampToWorkArea(this); positioned = true;
    }
}
