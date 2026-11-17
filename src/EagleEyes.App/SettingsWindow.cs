using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace EagleEyes.App;

internal sealed class SettingsWindow : Window
{
    internal SettingsWindow(Settings settings)
    {
        Title = "Eagle Eyes · Configurações"; Width = 580; Height = 640; WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Eagle Eyes", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        var output = Folder(panel, "Pasta das evidências", settings.OutputDirectory);
        var ffmpeg = Folder(panel, "Pasta com ffmpeg.exe e ffprobe.exe (vazio = localizar automaticamente)", settings.FfmpegDirectory);
        panel.Children.Add(new TextBlock { Text = "Monitor", Margin = new Thickness(0, 12, 0, 4) });
        var monitors = Native.Monitors(); var monitor = new ComboBox { ItemsSource = monitors.Select(m => $"{m.Device} · {m.Bounds.Width} × {m.Bounds.Height}").ToList(), SelectedIndex = Math.Max(0, monitors.FindIndex(m => m.Device == settings.MonitorDevice)) }; panel.Children.Add(monitor);
        var full = new CheckBox { Content = "Gravar monitor inteiro (desmarcado: selecionar região)", IsChecked = settings.FullMonitor, Margin = new Thickness(0, 16, 0, 8) }; panel.Children.Add(full);
        var pin = new CheckBox { Content = "Manter expandida (sem recolhimento automático)", IsChecked = settings.KeepExpanded, Margin = new Thickness(0, 0, 0, 8) }; panel.Children.Add(pin);
        var hide = new CheckBox { Content = "Ocultar barra durante a gravação (recomendado)", IsChecked = settings.HideDuringCapture, Margin = new Thickness(0, 0, 0, 8) }; panel.Children.Add(hide);
        panel.Children.Add(new TextBlock { Text = "O Eagle Eyes tenta excluir seus controles da captura. Como o resultado depende do Windows e do método de captura, ocultar a barra oferece comportamento previsível. Durante a pausa ela reaparece. Se desmarcar, os controles podem aparecer no vídeo.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "Ctrl+Shift+P  ·  pausar / retomar\nCtrl+Shift+F  ·  finalizar\n\nContagem regressiva: 3 segundos\nVídeo: 15 fps, cursor incluído, sem áudio.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var save = new Button { Content = "Salvar", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(22, 7, 22, 7) };
        save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(output.Text)) { MessageBox.Show("Informe a pasta de saída."); return; } settings.OutputDirectory = output.Text.Trim(); settings.FfmpegDirectory = ffmpeg.Text.Trim(); settings.FullMonitor = full.IsChecked == true; settings.KeepExpanded = pin.IsChecked == true; settings.HideDuringCapture = hide.IsChecked == true; if (monitor.SelectedIndex >= 0 && monitor.SelectedIndex < monitors.Count) settings.MonitorDevice = monitors[monitor.SelectedIndex].Device; try { settings.Save(); DialogResult = true; } catch (Exception ex) { MessageBox.Show(ex.Message, "Eagle Eyes"); } }; panel.Children.Add(save);
    }
    static TextBox Folder(Panel panel, string label, string initial)
    {
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
        var row = new DockPanel(); var browse = new Button { Content = "Escolher…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3) }; DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); var text = new TextBox { Text = initial, MinWidth = 200, VerticalContentAlignment = VerticalAlignment.Center }; row.Children.Add(text); panel.Children.Add(row);
        browse.Click += (_, _) => { var dialog = new OpenFolderDialog { Title = label }; if (dialog.ShowDialog() == true) text.Text = dialog.FolderName; }; return text;
    }
}
