using System.IO;
using System.Threading;
using System.Windows;
namespace EagleEyes.App;

internal sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "Local\\EagleEyes.Desktop", out var first);
        if (!first) { MessageBox.Show("O Eagle Eyes já está aberto.", "Eagle Eyes"); return; }
        var app = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { AppLog.Write("unhandled-ui", e.Exception); if(app.MainWindow is Window owner) UserMessages.Show(owner, "Não foi possível concluir esta ação. Tente novamente.", "Logs: " + AppLog.DirectoryPath); e.Handled = true; };
        Branding.Initialize();
        var window = new MainWindow(args); Branding.Apply(window);
        using var tray = new TrayIcon(window);
        var reportIndex = Array.IndexOf(args, "--branding-report");
        if (args.Contains("--ux-test") && reportIndex >= 0 && reportIndex + 1 < args.Length)
        {
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(new Action(() =>
            {
                var report = Path.GetFullPath(args[reportIndex + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new
                {
                    trayRegistered = tray.IsRegistered,
                    officialIconFile = Branding.IconFilePath,
                    iconFileExists = File.Exists(Branding.IconFilePath),
                    windowIconWidth = (window.Icon as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        app.Run(window);
    }
}
