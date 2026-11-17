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
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "Eagle Eyes"); e.Handled = true; };
        var window = new MainWindow(args); app.Run(window);
    }
}
