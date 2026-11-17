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
        var window = new MainWindow(args); app.Run(window);
    }
}
