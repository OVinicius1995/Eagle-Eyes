using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EagleEyes.Core;

namespace EagleEyes.App;

internal sealed class PrintPreview : Window
{
    readonly BitmapSource original;
    readonly CaptureArea area;
    readonly Native.RECT work;
    readonly DispatcherTimer timeout = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly Stopwatch animation = new();
    readonly TaskCompletionSource<BitmapSource> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly InkCanvas ink = new();
    bool editing;
    internal Task<BitmapSource> Result => result.Task;

    internal PrintPreview(BitmapSource image, CaptureArea area, Native.RECT work)
    {
        original = image;
        this.area = area;
        this.work = work;
        Title = "Editar print";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(27, 27, 43));
        var thumbnail = new Button { Content = new Image { Source = image, Stretch = Stretch.Uniform },
            Background = Background, BorderThickness = new Thickness(2), Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(thumbnail, "Editar print");
        thumbnail.Click += (_, _) => Edit();
        Content = thumbnail;
        SourceInitialized += (_, _) => { Native.TryExcludeFromCapture(this); Position(0); };
        Loaded += (_, _) =>
        {
            animation.Start();
            CompositionTarget.Rendering += Animate;
            timeout.Start();
        };
        timeout.Tick += (_, _) => Close();
        Closing += (_, _) => StopPreview();
        Closed += (_, _) => result.TrySetResult(original);
    }

    void Animate(object? sender, EventArgs args)
    {
        double fraction = Math.Min(1, animation.Elapsed.TotalMilliseconds / 230);
        Position(1 - Math.Pow(1 - fraction, 3));
        if (fraction >= 1) CompositionTarget.Rendering -= Animate;
    }

    void Position(double progress)
    {
        double dpi = Native.GetDpiForWindow(new WindowInteropHelper(this).Handle) / 96d;
        double fit = Math.Min(1, Math.Min(210 * dpi / area.Width, 145 * dpi / area.Height));
        int width = Math.Max(1, (int)(area.Width * fit)), height = Math.Max(1, (int)(area.Height * fit));
        int inset = (int)(16 * dpi);
        int Mix(int from, int to) => (int)Math.Round(from + (to - (double)from) * progress);
        Native.Place(this, Native.Rectangle(Mix(area.X, work.Right - inset - width), Mix(area.Y, work.Bottom - inset - height),
            Mix(area.Width, width), Mix(area.Height, height)));
    }

    void StopPreview()
    {
        timeout.Stop();
        CompositionTarget.Rendering -= Animate;
    }

    void Edit()
    {
        if (editing) return;
        editing = true;
        StopPreview();
        var layout = new DockPanel { Margin = new Thickness(10) };
        var tools = new WrapPanel();
        DockPanel.SetDock(tools, Dock.Top);
        layout.Children.Add(tools);
        void Add(string label, Action action)
        {
            var button = new Button { Content = label, Margin = new Thickness(3), Padding = new Thickness(8, 4, 8, 4) };
            button.Click += (_, _) => action();
            tools.Children.Add(button);
        }
        ink.Width = original.PixelWidth;
        ink.Height = original.PixelHeight;
        ink.Background = new ImageBrush(original) { Stretch = Stretch.Fill };
        ink.DefaultDrawingAttributes = new DrawingAttributes { Color = Colors.OrangeRed, Width = 5, Height = 5, IgnorePressure = true };
        Add("Vermelho", () => ink.DefaultDrawingAttributes.Color = Colors.OrangeRed);
        Add("Amarelo", () => ink.DefaultDrawingAttributes.Color = Colors.Gold);
        Add("Azul", () => ink.DefaultDrawingAttributes.Color = Colors.DeepSkyBlue);
        Add("Desfazer", () => { if (ink.Strokes.Count > 0) ink.Strokes.RemoveAt(ink.Strokes.Count - 1); });
        Add("Salvar print", FinishEdit);
        Add("Salvar original", Close);
        layout.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = ink });
        Content = layout;
        int width = (int)(work.Width * .8), height = (int)(work.Height * .8);
        Native.Place(this, Native.Rectangle(work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2, width, height));
        Activate();
    }

    internal void FinishForExit()
    {
        if (editing) FinishEdit();
        else Close();
    }

    void FinishEdit()
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(original, new Rect(0, 0, original.PixelWidth, original.PixelHeight));
            ink.Strokes.Draw(drawing);
        }
        var image = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        image.Freeze();
        result.TrySetResult(image);
        Close();
    }
}
