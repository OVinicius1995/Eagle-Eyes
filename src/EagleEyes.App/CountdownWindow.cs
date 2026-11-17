using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace EagleEyes.App;

internal sealed class CountdownWindow : Window
{
    readonly TextBlock number = new() { FontSize = 72, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Ellipse ring = new() { Width = 148, Height = 148, Stroke = new SolidColorBrush(Color.FromRgb(56, 213, 123)), StrokeThickness = 6, StrokeDashArray = new DoubleCollection { 74.35, 74.35 }, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(-90) };
    readonly CancellationTokenSource cancellation = new();
    bool escapeArmed;
    CountdownWindow(Native.Monitor monitor)
    {
        Title = "Eagle Eyes · Contagem";
        System.Windows.Automation.AutomationProperties.SetAutomationId(number, "CountdownValue"); WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(105, 0, 0, 0));
        var content = new StackPanel { Margin = new Thickness(30), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = "Preparando gravação", Foreground = Brushes.White, FontSize = 23, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 22) });
        var circle = new Grid { Width = 152, Height = 152 };
        circle.Children.Add(new Ellipse { Width = 148, Height = 148, Stroke = new SolidColorBrush(Color.FromRgb(57, 67, 65)), StrokeThickness = 6 });
        circle.Children.Add(ring); circle.Children.Add(number); content.Children.Add(circle);
        content.Children.Add(new TextBlock { Text = "Esc para cancelar", Foreground = new SolidColorBrush(Color.FromRgb(183, 185, 205)), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 27, 27, 43)), CornerRadius = new CornerRadius(18), Width = 330, Height = 326, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = content };
        Loaded += (_, _) => { Native.Place(this, monitor.Bounds); Native.TryExcludeFromCapture(this); Activate(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; escapeArmed = true; } else if (e.Key == Key.Space) e.Handled = true; };
        PreviewKeyUp += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; if (escapeArmed) cancellation.Cancel(); } else if (e.Key == Key.Space) e.Handled = true; };
        Closed += (_, _) => cancellation.Cancel();
    }
    internal static async Task<bool> RunAsync(Native.Monitor monitor)
    {
        var window = new CountdownWindow(monitor); bool complete = false;
        try
        {
            window.number.Text = "3"; window.Show(); window.Activate();
            for (int value = 3; value >= 1; value--)
            {
                window.number.Text = value.ToString();
                window.ring.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(0, 74.35, TimeSpan.FromSeconds(1)));
                await Task.Delay(1000, window.cancellation.Token);
            }
            complete = true;
        }
        catch (OperationCanceledException) { }
        finally { window.Close(); window.cancellation.Dispose(); }
        // Flush closing/layout before the caller starts screen capture.
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        return complete;
    }
}
