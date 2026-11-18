using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EagleEyes.App;

internal static class Branding
{
    static readonly Lazy<ImageSource> icon = new(() =>
    {
        var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/EagleEyes.App;component/Assets/eagle-eyes.ico"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderBy(f => f.PixelWidth).Last(); frame.Freeze(); return frame;
    });
    internal static ImageSource WindowIcon => icon.Value;
    internal static string IconFilePath => Path.Combine(AppContext.BaseDirectory, "Assets", "eagle-eyes.ico");
    internal static void Apply(Window window) => window.SetCurrentValue(Window.IconProperty, WindowIcon);
    internal static void Initialize()
    {
        // Covers preparation, countdown, settings and friendly dialogs as well as the primary window.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => { if (sender is Window window) Apply(window); }));
    }
}