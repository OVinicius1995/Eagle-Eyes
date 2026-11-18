using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace EagleEyes.App;

internal sealed class SettingsWindow : Window
{
    static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(183, 185, 205));
    internal SettingsWindow(Settings settings)
    {
        Title = "Eagle Eyes · Configurações"; Width = 568; Height = 648; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        Resources = (ResourceDictionary)XamlReader.Parse(ControlResources);
        var shell = new Grid();
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(27, 27, 43)), BorderBrush = new SolidColorBrush(Color.FromRgb(49, 51, 70)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = shell };

        var header = new Grid { Margin = new Thickness(26, 23, 18, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = BrandImage(); if (brand != null) { brand.Margin = new Thickness(0, 0, 14, 0); header.Children.Add(brand); }
        var headings = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headings.Children.Add(new TextBlock { Text = "Configurações", FontSize = 23, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        headings.Children.Add(new TextBlock { Text = "Eagle Eyes", FontSize = 12, Foreground = Ink, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(headings, 1); header.Children.Add(headings);
        var close = new Button { Content = FunctionalIcons.Create(FunctionalIcon.Close, 14), Width = 30, Height = 30, Background = Brushes.Transparent, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(close, "Fechar configurações"); close.Click += (_, _) => DialogResult = false; Grid.SetColumn(close, 2); header.Children.Add(close); shell.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(27, 0, 27, 0) };
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); shell.Children.Add(scroll);
        Section(body, "Gravação");
        Label(body, "Onde salvar seus vídeos");
        var folderRow = new Grid { Margin = new Thickness(0, 0, 0, 19) };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var output = new TextBox { Text = settings.OutputDirectory, Height = 38, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 6, 10, 6) };
        AutomationProperties.SetAutomationId(output, "OutputDirectory"); AutomationProperties.SetName(output, "Pasta de saída"); folderRow.Children.Add(output);
        var browse = new Button { Content = "Escolher…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(13, 7, 13, 7) };
        AutomationProperties.SetName(browse, "Escolher pasta de saída"); Grid.SetColumn(browse, 1); folderRow.Children.Add(browse); body.Children.Add(folderRow);
        browse.Click += (_, _) => { var dialog = new OpenFolderDialog { Title = "Escolha onde salvar os vídeos" }; if (dialog.ShowDialog(this) == true) output.Text = dialog.FolderName; };

        var choices = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var modeColumn = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; Label(modeColumn, "Área de gravação");
        var mode = new ComboBox { Height = 38, ItemsSource = new[] { "Selecionar uma região", "Monitor inteiro" }, SelectedIndex = settings.FullMonitor ? 1 : 0 };
        AutomationProperties.SetAutomationId(mode, "CaptureMode"); AutomationProperties.SetName(mode, "Área de gravação"); modeColumn.Children.Add(mode); choices.Children.Add(modeColumn);
        var monitorColumn = new StackPanel { Margin = new Thickness(10, 0, 0, 0) }; Label(monitorColumn, "Monitor");
        var monitors = Native.Monitors();
        var monitor = new ComboBox { Height = 38, ItemsSource = monitors.Select((m, i) => $"Monitor {i + 1} · {m.Bounds.Width} × {m.Bounds.Height}").ToArray(), SelectedIndex = monitors.Count == 0 ? -1 : Math.Max(0, monitors.FindIndex(m => m.Device == settings.MonitorDevice)), IsEnabled = settings.FullMonitor };
        AutomationProperties.SetAutomationId(monitor, "MonitorChoice"); AutomationProperties.SetName(monitor, "Monitor"); monitorColumn.Children.Add(monitor); Grid.SetColumn(monitorColumn, 1); choices.Children.Add(monitorColumn); body.Children.Add(choices);
        mode.SelectionChanged += (_, _) => monitor.IsEnabled = mode.SelectedIndex == 1;

        Section(body, "Barra flutuante");
        var pin = new CheckBox { Content = "Manter os controles expandidos", IsChecked = settings.KeepExpanded, Margin = new Thickness(0, 0, 0, 10), Foreground = Brushes.White };
        AutomationProperties.SetAutomationId(pin, "KeepExpanded"); body.Children.Add(pin);
        body.Children.Add(new TextBlock { Text = "Desative para recolher a barra quando ela estiver ociosa. Você pode arrastá-la para outra borda a qualquer momento.", TextWrapping = TextWrapping.Wrap, Foreground = Ink, FontSize = 12, LineHeight = 19, Margin = new Thickness(25, 0, 0, 21) });

        var shortcuts = new StackPanel { Margin = new Thickness(14, 11, 14, 11) };
        shortcuts.Children.Add(new TextBlock { Text = "Atalhos durante a gravação", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
        Shortcut(shortcuts, "Pausar ou retomar", "Ctrl + Shift + P"); Shortcut(shortcuts, "Parar e salvar", "Ctrl + Shift + F");
        body.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(34, 35, 52)), CornerRadius = new CornerRadius(9), Child = shortcuts });
        body.Children.Add(new TextBlock { Text = "15 fps · Cursor incluído · Sem áudio", Foreground = Ink, FontSize = 11, Margin = new Thickness(0, 15, 0, 14) });

        var footer = new Grid { Margin = new Thickness(27, 17, 27, 24) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Padding = new Thickness(19, 9, 19, 9), Margin = new Thickness(0, 0, 10, 0) }; cancel.Click += (_, _) => DialogResult = false; Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
        var save = new Button { Content = "Salvar", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(43, 124, 83)), BorderBrush = new SolidColorBrush(Color.FromRgb(58, 155, 105)), Padding = new Thickness(23, 9, 23, 9) }; Grid.SetColumn(save, 2); footer.Children.Add(save); Grid.SetRow(footer, 2); shell.Children.Add(footer);
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(output.Text)) { UserMessages.Show(this, "Escolha a pasta onde seus vídeos serão salvos."); output.Focus(); return; }
            var previousOutput = settings.OutputDirectory; var previousFull = settings.FullMonitor; var previousPin = settings.KeepExpanded; var previousMonitor = settings.MonitorDevice;
            settings.OutputDirectory = output.Text.Trim(); settings.FullMonitor = mode.SelectedIndex == 1; settings.KeepExpanded = pin.IsChecked == true;
            if (monitor.SelectedIndex >= 0 && monitor.SelectedIndex < monitors.Count) settings.MonitorDevice = monitors[monitor.SelectedIndex].Device;
            try { settings.Save(); DialogResult = true; }
            catch (Exception error) { settings.OutputDirectory = previousOutput; settings.FullMonitor = previousFull; settings.KeepExpanded = previousPin; settings.MonitorDevice = previousMonitor; AppLog.Write("settings-save", error); UserMessages.Show(this, "Não foi possível salvar as configurações. Tente novamente.", error.Message); }
        };
        WindowDragBehavior.Attach(this); Loaded += (_, _) => Native.ClampToWorkArea(this);
    }
    static Image? BrandImage()
    {
        try { return new Image { Source = new BitmapImage(new Uri("pack://application:,,,/EagleEyes.App;component/Assets/eagle-eyes.png")), Width = 46, Height = 46, Stretch = Stretch.Uniform }; }
        catch { return null; }
    }
    static void Section(Panel panel, string title) => panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 5, 0, 15) });
    static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Foreground = Ink, FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
    static void Shortcut(Panel panel, string label, string keys)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, Foreground = Ink, FontSize = 12 }); var shortcut = new TextBlock { Text = keys, Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 12 }; Grid.SetColumn(shortcut, 1); row.Children.Add(shortcut); panel.Children.Add(row);
    }
    const string ControlResources = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="Button">
    <Setter Property="Foreground" Value="#F1F2F8"/><Setter Property="Background" Value="#2B2C40"/><Setter Property="BorderBrush" Value="#414359"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="12"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="Border" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="6" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Border" Property="Opacity" Value=".8"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#B7B9CD"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Border" Property="Opacity" Value=".4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="TextBox">
    <Setter Property="Foreground" Value="#F1F2F8"/><Setter Property="CaretBrush" Value="White"/><Setter Property="Background" Value="#232436"/><Setter Property="BorderBrush" Value="#414359"/><Setter Property="BorderThickness" Value="1"/><Setter Property="FontSize" Value="12"/><Setter Property="SelectionBrush" Value="#397F62"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Border" CornerRadius="6" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><ScrollViewer x:Name="PART_ContentHost" Margin="0"/></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#8E96B7"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="#F1F2F8"/><Setter Property="Padding" Value="10,9"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="Item" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Item" Property="Background" Value="#3A3D55"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Item" Property="Background" Value="#34394E"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="#F1F2F8"/><Setter Property="Background" Value="#232436"/><Setter Property="BorderBrush" Value="#414359"/><Setter Property="FontSize" Value="12"/><Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton x:Name="Toggle" Focusable="False" IsChecked="{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}" ClickMode="Press"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="#232436" BorderBrush="#414359" BorderThickness="1" CornerRadius="6"><Path Data="M0,0 L4,4 L8,0" Stroke="#B7B9CD" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="10,0,31,0" VerticalAlignment="Center"/><Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade"><Border Background="#282A3D" BorderBrush="#414359" BorderThickness="1" CornerRadius="6" MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="240"><ScrollViewer Margin="3"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value=".45"/></Trigger><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter Property="BorderBrush" Value="#8E96B7"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="CheckBox">
    <Setter Property="FontSize" Value="13"/><Setter Property="Cursor" Value="Hand"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="25"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border x:Name="Box" Width="17" Height="17" HorizontalAlignment="Left" Background="#232436" BorderBrush="#70748F" BorderThickness="1" CornerRadius="4"/><Path x:Name="Mark" Visibility="Collapsed" Data="M3,8 L7,12 L14,4" Width="17" Height="17" HorizontalAlignment="Left" Stroke="White" StrokeThickness="1.8" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/><ContentPresenter Grid.Column="1" VerticalAlignment="Center"/></Grid><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Box" Property="Background" Value="#2B7C53"/><Setter TargetName="Box" Property="BorderBrush" Value="#42996E"/><Setter TargetName="Mark" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="#B7B9CD"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
</ResourceDictionary>
""";
}





