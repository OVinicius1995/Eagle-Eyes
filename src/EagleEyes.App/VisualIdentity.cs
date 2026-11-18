using System.Windows;

namespace EagleEyes.App;

internal static class VisualIdentity
{
    internal static ResourceDictionary CreateResources() => new()
    {
        Source = new Uri("pack://application:,,,/EagleEyes.App;component/Design/EagleEyes.Theme.xaml", UriKind.Absolute)
    };

    internal static Style ButtonStyle(ResourceDictionary resources, bool primary) =>
        (Style)resources[primary ? "EagleEyes.Button.Primary" : "EagleEyes.Button.Secondary"];
}
