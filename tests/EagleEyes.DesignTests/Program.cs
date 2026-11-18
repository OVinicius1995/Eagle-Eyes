using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Program
{
    static readonly List<string> checks = [];
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); Console.WriteLine("PASS " + label); }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;
    static void SavePreview(Window window, string path)
    {
        var content = (FrameworkElement)window.Content; content.UpdateLayout();
        var margin = content.Margin;
        int width = (int)Math.Ceiling(content.ActualWidth + margin.Left + margin.Right), height = (int)Math.Ceiling(content.ActualHeight + margin.Top + margin.Bottom);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(margin.Left, margin.Top, content.ActualWidth, content.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
    [STAThread]
    static int Main()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string artifacts = Path.Combine(root, ".test-artifacts", "design-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(artifacts);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window? owner = null; Window? settings = null; Exception? failure = null;
        try
        {
            var assembly = Assembly.Load("EagleEyes.App");
            var settingsType = assembly.GetType("EagleEyes.App.Settings", true)!;
            var windowType = assembly.GetType("EagleEyes.App.SettingsWindow", true)!;
            var model = Activator.CreateInstance(settingsType, true)!;
            settings = (Window)Activator.CreateInstance(windowType, BindingFlags.Instance | BindingFlags.NonPublic, null, [model], null)!;
            var settingsContent = (FrameworkElement)settings.Content;
            settingsContent.Measure(new Size(568, 648)); settingsContent.Arrange(new Rect(0, 0, 568, 648)); settingsContent.UpdateLayout();
            var settingsSave = Descendants<Button>(settingsContent).Single(b => b.Content as string == "Salvar");
            Assert(settings.Resources.Source!.OriginalString.EndsWith("Design/EagleEyes.Theme.xaml"), "Settings uses shared theme resource");
            Assert(ColorOf(settingsSave.Background) == Color.FromRgb(43, 124, 83), "Settings primary keeps approved green");
            SavePreview(settings, Path.Combine(artifacts, "settings.png"));
            var updateToggle = Descendants<CheckBox>(settingsContent).Single(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "CheckForUpdates");
            Assert(updateToggle.IsChecked == true, "Automatic update notice defaults enabled");
            Assert(Descendants<Button>(settingsContent).Any(b => b.Content as string == "Verificar atualizações" && b.IsEnabled), "Manual update check available");
            Descendants<ScrollViewer>(settingsContent).First().ScrollToBottom(); settingsContent.UpdateLayout();
            SavePreview(settings, Path.Combine(artifacts, "settings-updates.png"));

            owner = new Window { Width = 1, Height = 1, Left = 10, Top = 10, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
            owner.Show(); owner.Hide();
            var saved = assembly.GetType("EagleEyes.App.UserMessages", true)!.GetMethod("Saved", BindingFlags.NonPublic | BindingFlags.Static)!;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) =>
            {
                timer.Stop(); Window? dialog = null;
                try
                {
                    dialog = app.Windows.Cast<Window>().First(w => w != owner && w.IsVisible && w.Title == "Eagle Eyes");
                    dialog.UpdateLayout();
                    var buttons = Descendants<Button>((DependencyObject)dialog.Content).ToArray();
                    var open = buttons.Single(b => b.Content as string == "Abrir pasta");
                    var done = buttons.Single(b => b.Content as string == "Concluir");
                    Assert(buttons.Length == 2, "Saved dialog retains two real actions");
                    Assert(dialog.Resources.Source == settings.Resources.Source, "Saved dialog and settings share one theme");
                    Assert(ReferenceEquals(open.Style, dialog.Resources["EagleEyes.Button.Secondary"]), "Open folder uses secondary style");
                    Assert(ReferenceEquals(done.Style, dialog.Resources["EagleEyes.Button.Primary"]), "Done uses primary style");
                    Assert(ColorOf(open.Background) == Color.FromRgb(43, 44, 64), "Secondary button uses approved dark surface");
                    Assert(ColorOf(done.Background) == ColorOf(settingsSave.Background), "Done and Save use the same green");
                    Assert(ColorOf(done.BorderBrush) == Color.FromRgb(58, 155, 105), "Primary border follows palette");
                    Assert(ColorOf(done.Foreground) == Color.FromRgb(241, 242, 248), "Button text follows palette");
                    Assert(done.FontSize == 12 && open.FontSize == 12, "Button font size is consistent");
                    Assert(done.Padding == new Thickness(19, 9, 19, 9) && open.Padding == done.Padding, "Actions share padding");
                    Assert(done.MinHeight == 36 && open.MinHeight == 36, "Actions preserve minimum hit height");
                    Assert(done.IsDefault && done.IsCancel, "Done preserves default and cancel keyboard roles");
                    Assert(done.IsEnabled && open.IsEnabled && done.Focusable && open.Focusable, "Actions remain enabled and keyboard focusable");
                    open.ApplyTemplate(); done.ApplyTemplate();
                    var border = (Border)done.Template.FindName("Border", done);
                    Assert(border.CornerRadius == new CornerRadius(6), "Dialog uses rounded6DIP action template");
                    Assert(open.Template.FindName("Border", open) is Border, "Open folder uses same rounded template family");
                    Assert(done.Template.Triggers.Cast<Trigger>().Any(t => t.Property == UIElement.IsKeyboardFocusedProperty), "Shared template includes keyboard focus feedback");
                    SavePreview(dialog, Path.Combine(artifacts, "saved-dialog.png"));
                    done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(!dialog.IsVisible, "Concluir actually closes the saved dialog");
                }
                catch (Exception error) { failure = error; dialog?.Close(); }
            };
            timer.Start();
            saved.Invoke(null, [owner, Path.Combine(artifacts, "Evidencia-de-teste.mp4")]);
            if (failure != null) throw failure;
            var releaseType = assembly.GetType("EagleEyes.App.UpdateRelease", true)!;
            var release = Activator.CreateInstance(releaseType, [new Version(1, 3, 0), new Uri("https://github.com/OVinicius1995/Eagle-Eyes/releases/tag/v1.3.0")]);
            var notice = assembly.GetType("EagleEyes.App.UserMessages", true)!.GetMethod("UpdateAvailable", BindingFlags.NonPublic | BindingFlags.Static)!;
            var noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            noticeTimer.Tick += (_, _) =>
            {
                noticeTimer.Stop(); Window? dialog = null;
                try
                {
                    dialog = app.Windows.Cast<Window>().Single(w => w.Title == "Nova versão disponível" && w.IsVisible);
                    var later = Descendants<Button>((DependencyObject)dialog.Content).Single(b => b.Content as string == "Agora não");
                    var open = Descendants<Button>((DependencyObject)dialog.Content).Single(b => b.Content as string == "Abrir versão");
                    Assert(later.IsCancel, "Update notice supports dismissal");
                    Assert(ReferenceEquals(open.Style, dialog.Resources["EagleEyes.Button.Primary"]), "Update action follows shared identity");
                    SavePreview(dialog, Path.Combine(artifacts, "update-notice.png"));
                    later.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(!dialog.IsVisible, "Dismiss update closes dialog without browser or installation");
                }
                catch (Exception error) { failure = error; dialog?.Close(); }
            };
            noticeTimer.Start(); notice.Invoke(null, [owner, release]);
            if (failure != null) throw failure;
            Assert(checks.Count >= 19, "Dialog rendering checks completed");
            var guidePath = Path.Combine(root, "docs", "IDENTIDADE-VISUAL.md");
            if (File.Exists(guidePath))
            {
                string guide = File.ReadAllText(guidePath);
                Assert(guide.Contains("#1B1B2B") && guide.Contains("#B7B9CD") && guide.Contains("EagleEyes.Theme.xaml"), "Local private guide records reusable theme");
            }
            else Console.WriteLine("SKIP private identity guide is intentionally not distributed");
            return 0;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); return 1; }
        finally
        {
            owner?.Close(); settings?.Close();
            File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { Passed = checks.Count, Checks = checks, Failure = failure?.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("ARTIFACT " + artifacts); app.Shutdown();
        }
    }
}
