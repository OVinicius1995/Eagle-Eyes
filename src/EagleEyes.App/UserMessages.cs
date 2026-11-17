using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EagleEyes.App;

internal static class AppLog
{
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EagleEyes", "logs");
    internal static void Write(string operation, Exception? error = null, string? details = null)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(Path.Combine(DirectoryPath, DateTime.Now.ToString("yyyy-MM-dd") + ".log"), $"{DateTimeOffset.Now:O} [{operation}] {details}\n{error}\n");
        }
        catch { /* Logging must not prevent saving or restoring the UI. */ }
    }
}

internal static class UserMessages
{
    internal static void Show(Window owner, string message, string? details = null)
    {
        var (window, panel, actions) = Create(owner, "Eagle Eyes", message);
        if (!string.IsNullOrWhiteSpace(details))
        {
            panel.Children.Insert(1, new Expander
            {
                Header = "Ver detalhes", Margin = new Thickness(0, 14, 0, 0),
                Content = new TextBox { Text = details, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });
        }
        var ok = Button("Entendi"); ok.IsDefault = true; ok.IsCancel = true;
        ok.Click += (_, _) => window.DialogResult = true; actions.Children.Add(ok);
        window.ShowDialog();
    }

    internal static bool ConfirmDiscard(Window owner)
    {
        var (window, _, actions) = Create(owner, "Descartar gravação?", "O vídeo desta gravação será apagado. Essa ação não pode ser desfeita.");
        var keep = Button("Continuar gravação"); keep.IsDefault = true; keep.IsCancel = true;
        keep.Click += (_, _) => window.DialogResult = false;
        var discard = Button("Descartar gravação"); discard.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(keep); actions.Children.Add(discard);
        return window.ShowDialog() == true;
    }

    internal static void Saved(Window owner, string output)
    {
        var (window, panel, actions) = Create(owner, "Eagle Eyes", "Seu vídeo foi salvo!");
        panel.Children.Insert(1, new TextBlock { Text = Path.GetFileName(output), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), Foreground = Brushes.LightGray });
        var open = Button("Abrir pasta");
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + output + "\"", UseShellExecute = true }); }
            catch (Exception ex) { AppLog.Write("open-folder", ex); Show(window, "Não foi possível abrir a pasta. Você pode acessá-la pelo Explorador de Arquivos.", output); }
        };
        var done = Button("Concluir"); done.IsDefault = true; done.IsCancel = true;
        done.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(open); actions.Children.Add(done);
        window.ShowDialog();
    }

    static (Window Window, StackPanel Panel, StackPanel Actions) Create(Window owner, string title, string message)
    {
        var window = new Window { Title = title, Owner = owner, Width = 490, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Background = new SolidColorBrush(Color.FromRgb(27, 27, 43)), Foreground = Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, FontSize = 16, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        panel.Children.Add(actions); window.Content = panel;
        window.SourceInitialized += (_, _) => Native.TryExcludeFromCapture(window);
        return (window, panel, actions);
    }
    static Button Button(string text) => new() { Content = text, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(6, 0, 0, 0) };
}
