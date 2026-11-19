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
        var ok = Button("Entendi", window.Resources, true); ok.IsDefault = true; ok.IsCancel = true;
        ok.Click += (_, _) => window.DialogResult = true; actions.Children.Add(ok);
        window.ShowDialog();
    }

    internal static FileStream? UpdateAvailable(Window owner, UpdateRelease release)
    {
        var (window, panel, actions) = Create(owner, "Nova versão disponível", $"Atualizar o Eagle Eyes para {release.Version.ToString(3)}? Depois de conferir o download, o aplicativo fechará e iniciará a instalação.");
        using var cancel = new CancellationTokenSource();
        var later = Button("Agora não", window.Resources);
        later.IsCancel = true;
        later.Click += (_, _) => window.Close();
        var install = Button("Atualizar", window.Resources, true);
        install.IsDefault = true;
        FileStream? installer = null;
        bool downloading = false;
        var status = (TextBlock)panel.Children[0];
        window.Closing += (_, e) =>
        {
            if (!downloading) return;
            e.Cancel = true;
            cancel.Cancel();
            status.Text = "Cancelando download…";
        };
        install.Click += async (_, _) =>
        {
            if (downloading) return;
            downloading = true;
            install.IsEnabled = false;
            later.Content = "Cancelar";
            status.Text = "Baixando e verificando o instalador…";
            try
            {
                installer = await UpdateService.DownloadAsync(release, cancel.Token);
                downloading = false;
                window.DialogResult = !cancel.IsCancellationRequested;
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                downloading = false;
                window.Close();
            }
            catch (Exception error)
            {
                AppLog.Write("update-download", error);
                downloading = false;
                install.IsEnabled = true;
                later.Content = "Agora não";
                status.Text = "O download não pôde ser concluído e verificado. Confira a conexão e clique em Atualizar para tentar novamente.";
            }
        };
        actions.Children.Add(later);
        actions.Children.Add(install);
        if (window.ShowDialog() == true) return installer;
        installer?.Dispose();
        return null;
    }
    internal static bool ConfirmDiscard(Window owner)
    {
        var (window, _, actions) = Create(owner, "Descartar gravação?", "O vídeo desta gravação será apagado. Essa ação não pode ser desfeita.");
        var keep = Button("Continuar gravação", window.Resources, true); keep.IsDefault = true; keep.IsCancel = true;
        keep.Click += (_, _) => window.DialogResult = false;
        var discard = Button("Descartar gravação", window.Resources); discard.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(keep); actions.Children.Add(discard);
        return window.ShowDialog() == true;
    }

    internal static void Saved(Window owner, string output, string? remainingDirectory = null)
    {
        var (window, panel, actions) = Create(owner, "Eagle Eyes", "Seu vídeo foi salvo!");
        panel.Children.Insert(1, new TextBlock { Text = Path.GetFileName(output), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), Foreground = (Brush)window.Resources["EagleEyes.Text.Muted"] });
        if (remainingDirectory != null)
            panel.Children.Insert(2, new TextBlock { Text = $"Alguns arquivos de trabalho ficaram nesta pasta: {remainingDirectory}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        var open = Button("Abrir pasta", window.Resources);
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + output + "\"", UseShellExecute = true }); }
            catch (Exception ex) { AppLog.Write("open-folder", ex); Show(window, "Não foi possível abrir a pasta. Você pode acessá-la pelo Explorador de Arquivos.", output); }
        };
        var done = Button("Concluir", window.Resources, true); done.IsDefault = true; done.IsCancel = true;
        done.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(open); actions.Children.Add(done);
        window.ShowDialog();
    }

    static (Window Window, StackPanel Panel, StackPanel Actions) Create(Window owner, string title, string message)
    {
        var window = new Window { Title = title, Owner = owner, Width = 490, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Background = new SolidColorBrush(Color.FromRgb(27, 27, 43)), Foreground = Brushes.White };
        window.Resources = VisualIdentity.CreateResources();
        window.Background = (Brush)window.Resources["EagleEyes.Surface.Window"];
        window.Foreground = (Brush)window.Resources["EagleEyes.Text.Primary"];
        window.FontFamily = new FontFamily("Segoe UI");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, FontSize = 16, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        panel.Children.Add(actions); window.Content = panel;
        window.SourceInitialized += (_, _) => Native.TryExcludeFromCapture(window);
        return (window, panel, actions);
    }
    static Button Button(string text, ResourceDictionary resources, bool primary = false) => new() { Content = text, Style = VisualIdentity.ButtonStyle(resources, primary), Margin = new Thickness(8, 0, 0, 0) };
}
