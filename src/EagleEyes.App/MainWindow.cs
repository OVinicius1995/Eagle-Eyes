using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using System.Windows.Threading;
using EagleEyes.Core;
namespace EagleEyes.App;

internal sealed class MainWindow : Window
{
    readonly Settings settings = Settings.Load();
    readonly string[] args;
    readonly Canvas canvas = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer collapseTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    readonly Brush ink = new SolidColorBrush(Color.FromRgb(183, 185, 205));
    readonly Brush dark = new SolidColorBrush(Color.FromRgb(27, 27, 43));
    RecordingSession? session; bool busy, closing, hotkeys, animating; string transient = "";
    TextBlock? duration;
    Native.Monitor monitor = null!;
    bool Testing => args.Contains("--smoke-test") || args.Contains("--proof-recording");
    bool Vertical => settings.Edge is "Left" or "Right";
    bool Active => session?.State is RecordingState.Recording or RecordingState.Paused or RecordingState.Finalizing;
    IntPtr Handle => new WindowInteropHelper(this).Handle;
    internal MainWindow(string[] args)
    {
        this.args = args; Title = "Eagle Eyes"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; Content = canvas;
        var available = Native.Monitors(); if (available.Count == 0) throw new InvalidOperationException("Nenhum monitor disponível."); monitor = available.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? available[0]; settings.MonitorDevice = monitor.Device;
        if (settings.Edge is not ("Left" or "Right" or "Top" or "Bottom")) settings.Edge = "Right";
        SourceInitialized += (_, _) => { var source = HwndSource.FromHwnd(Handle); source?.AddHook(Hook); hotkeys = Native.RegisterHotKey(Handle, 1, 0x4006, 0x50); hotkeys = Native.RegisterHotKey(Handle, 2, 0x4006, 0x46) && hotkeys; Native.SetWindowDisplayAffinity(Handle, 0x11); };
        Loaded += async (_, _) => { Render(); Dock(); if (args.Contains("--smoke-test")) { await Task.Delay(800); await SmokeCycle(); closing = true; Close(); } else if (args.Contains("--proof-recording")) await RunProof(); };
        Closing += (_, e) => { if (!closing && (busy || Active)) { e.Cancel = true; MessageBox.Show("Finalize a gravação antes de fechar o Eagle Eyes. Use Ctrl+Shift+F ou o botão Finalizar.", "Eagle Eyes"); return; } Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2); timer.Stop(); collapseTimer.Stop(); session?.Dispose(); if (!Testing) { try { settings.Save(); } catch { } }; };
        timer.Tick += (_, _) => { if (duration != null) duration.Text = transient.Length > 0 ? transient : DurationText(); if (session?.State == RecordingState.Faulted && !busy) { Show(); ReleaseFault(new IOException("A captura foi interrompida. Os trechos disponíveis foram preservados; quadros ausentes não podem ser recuperados.")); transient = ""; Render(); } };
        timer.Start();
        collapseTimer.Tick += async (_, _) => { collapseTimer.Stop(); if (!settings.KeepExpanded && !IsMouseOver && !busy && !Active && settings.Expanded) await ToggleExpanded(); };
        MouseEnter += (_, _) => collapseTimer.Stop(); MouseLeave += (_, _) => { if (!settings.KeepExpanded) collapseTimer.Start(); };
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x312) { handled = true; if (w.ToInt32() == 1) _ = PauseResume(); else if (w.ToInt32() == 2) _ = Stop(); }
        if (msg == 0x2e0) Dispatcher.BeginInvoke(Dock);
        if (msg == 0x7e && !Active && !busy) Dispatcher.BeginInvoke(() => { var all = Native.Monitors(); if (all.Count > 0) { monitor = all.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? all[0]; settings.MonitorDevice = monitor.Device; Dock(); } });
        return IntPtr.Zero;
    }
    Button IconButton(string tip, string geometry, Action action, double size = 32)
    {
        var button = new Button { Width = size, Height = size, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = ink, Cursor = Cursors.Hand, ToolTip = tip, Padding = new Thickness(6), Focusable = true };
        var border = new FrameworkElementFactory(typeof(Border)); border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter); var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(45, 183, 185, 205)))); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(65, 183, 185, 205)))); template.Triggers.Add(focus); button.Template = template;
        button.Content = new Viewbox { Width = size - 12, Height = size - 12, Child = new Path { Data = Geometry.Parse(geometry), Stroke = ink, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.Uniform } };
        AutomationProperties.SetName(button, tip); button.Click += (_, _) => action(); return button;
    }
    void Render()
    {
        canvas.Children.Clear(); bool expanded = settings.Expanded; double length = Active || busy ? 206 : 126; double width = Vertical ? (expanded ? 100 : 38) : (expanded ? length : 58), height = Vertical ? (expanded ? length : 58) : (expanded ? 100 : 38); Width = width; Height = height;
        Geometry silhouette;
        if (!expanded) silhouette = new RectangleGeometry(new Rect(0, 0, width, height), 16, 16);
        else
        {
            double c = length / 2; var raw = Geometry.Parse($"M100,0 L50,0 Q38,0 38,12 L38,{c - 39} Q38,{c - 29} 28,{c - 29} L16,{c - 29} Q0,{c - 29} 0,{c - 13} L0,{c + 13} Q0,{c + 29} 16,{c + 29} L28,{c + 29} Q38,{c + 29} 38,{c + 39} L38,{length - 12} Q38,{length} 50,{length} L100,{length} Z");
            raw = raw.Clone();
            if (settings.Edge == "Left") raw.Transform = new MatrixTransform(-1, 0, 0, 1, 100, 0);
            if (settings.Edge == "Top") raw.Transform = new MatrixTransform(0, -1, 1, 0, 0, 100);
            if (settings.Edge == "Bottom") raw.Transform = new MatrixTransform(0, 1, -1, 0, length, 0);
            silhouette = raw;
        }
        canvas.Clip = silhouette; canvas.Children.Add(new Path { Data = silhouette, Fill = dark });
        var arrow = IconButton(expanded ? "Recolher barra" : "Expandir barra", "M2,1 L10,9 L2,17", () => _ = ToggleExpanded(), 34);
        arrow.RenderTransformOrigin = new Point(.5, .5); double angle = settings.Edge switch { "Left" => 180, "Top" => -90, "Bottom" => 90, _ => 0 }; if (!expanded) angle += 180; arrow.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(arrow, Vertical ? (expanded ? (settings.Edge == "Right" ? 2 : 64) : 2) : (width - 34) / 2); Canvas.SetTop(arrow, Vertical ? (height - 34) / 2 : (expanded ? (settings.Edge == "Top" ? 64 : 2) : 2)); canvas.Children.Add(arrow);
        if (!expanded) { duration = null; Dock(); return; }
        var body = new StackPanel { Orientation = Vertical ? Orientation.Vertical : Orientation.Horizontal, Width = Vertical ? 62 : length, Height = Vertical ? length : 62, VerticalAlignment = VerticalAlignment.Center };
        Canvas.SetLeft(body, settings.Edge == "Right" ? 38 : 0); Canvas.SetTop(body, settings.Edge == "Bottom" ? 38 : 0); canvas.Children.Add(body);
        var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3) };
        top.Children.Add(IconButton("Configurações", "M8,1 L12,1 L13,4 L16,6 L19,6 L21,10 L19,13 L19,16 L16,19 L13,19 L10,21 L6,19 L6,16 L3,13 L1,10 L3,6 L6,6 Z M14,10 A4,4 0 1 1 6,10 A4,4 0 1 1 14,10", Configure, 28));
        top.Children.Add(IconButton("Fechar Eagle Eyes", "M2,2 L18,18 M18,2 L2,18", Close, 28)); body.Children.Add(top);
        var drag = new Border { Width = Vertical ? 54 : 12, Height = Vertical ? 12 : 54, Background = Brushes.Transparent, Cursor = Cursors.SizeAll, ToolTip = "Arraste para mover e encaixar na borda", Child = new Path { Data = Geometry.Parse(Vertical ? "M17,3 L35,3 M17,7 L35,7" : "M3,17 L3,35 M7,17 L7,35"), Stroke = ink, StrokeThickness = 1 } };
        drag.MouseLeftButtonDown += (_, _) => { if (busy) return; try { DragMove(); SnapAfterDrag(); } catch (InvalidOperationException) { } }; body.Children.Add(drag);
        if (Active || busy)
        {
            var state = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
            state.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = session?.State == RecordingState.Paused ? Brushes.Goldenrod : Brushes.OrangeRed, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
            duration = new TextBlock { Foreground = ink, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Text = transient.Length > 0 ? transient : DurationText(), ToolTip = session?.State == RecordingState.Paused ? "Pausada · tempo gravado" : "Tempo efetivamente gravado" }; state.Children.Add(duration); body.Children.Add(state);
            if (!busy) { body.Children.Add(IconButton(session?.State == RecordingState.Paused ? "Retomar · Ctrl+Shift+P" : "Pausar · Ctrl+Shift+P", session?.State == RecordingState.Paused ? "M4,2 L18,10 L4,18 Z" : "M5,2 L5,18 M15,2 L15,18", () => _ = PauseResume(), 38)); body.Children.Add(IconButton("Finalizar · Ctrl+Shift+F", "M3,3 L17,3 L17,17 L3,17 Z", () => _ = Stop(), 38)); }
        }
        else { duration = null; body.Children.Add(IconButton("Gravar vídeo", "M2,4 L14,4 L14,16 L2,16 Z M14,8 L21,4 L21,16 L14,12", () => _ = Start(), 42)); }
        Dock();
    }
    void Dock()
    {
        if (Handle == IntPtr.Zero) return; var all = Native.Monitors(); if (all.Count == 0) return; monitor = all.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? all[0]; settings.MonitorDevice = monitor.Device;
        double scale = Math.Max(1, Native.GetDpiForWindow(Handle) / 96d); int width = (int)Math.Round(Width * scale), height = (int)Math.Round(Height * scale); var w = monitor.Work; double offset = double.IsFinite(settings.Offset) ? Math.Clamp(settings.Offset, 0, 1) : 0.5;
        int x = w.Left + (int)((w.Width - width) * offset), y = w.Top + (int)((w.Height - height) * offset);
        if (settings.Edge == "Right") x = w.Right - width; if (settings.Edge == "Left") x = w.Left; if (settings.Edge == "Top") y = w.Top; if (settings.Edge == "Bottom") y = w.Bottom - height;
        x = Math.Clamp(x, w.Left, Math.Max(w.Left, w.Right - width)); y = Math.Clamp(y, w.Top, Math.Max(w.Top, w.Bottom - height)); Native.Place(this, Native.Rectangle(x, y, width, height));
    }
    void SnapAfterDrag()
    {
        Native.GetWindowRect(Handle, out var r); var all = Native.Monitors(); monitor = all.OrderBy(m => Distance(m.Work, r)).First(); settings.MonitorDevice = monitor.Device; var w = monitor.Work;
        var sides = new[] { ("Left", Math.Abs(r.Left - w.Left)), ("Right", Math.Abs(r.Right - w.Right)), ("Top", Math.Abs(r.Top - w.Top)), ("Bottom", Math.Abs(r.Bottom - w.Bottom)) }; settings.Edge = sides.MinBy(v => v.Item2).Item1;
        settings.Offset = Vertical ? (r.Top - w.Top) / (double)Math.Max(1, w.Height - r.Height) : (r.Left - w.Left) / (double)Math.Max(1, w.Width - r.Width); settings.Offset = Math.Clamp(settings.Offset, 0, 1); Render(); Save();
    }
    static long Distance(Native.RECT m, Native.RECT r) { long x = (r.Left + r.Right) / 2, y = (r.Top + r.Bottom) / 2; long dx = Math.Max(m.Left - x, Math.Max(0, x - m.Right)), dy = Math.Max(m.Top - y, Math.Max(0, y - m.Bottom)); return dx * dx + dy * dy; }
    async Task ToggleExpanded()
    {
        if (animating || busy) return; animating = true;
        if (settings.Expanded) { var move = new TranslateTransform(); canvas.RenderTransform = move; var animation = new DoubleAnimation(0, settings.Edge is "Right" or "Bottom" ? 62 : -62, TimeSpan.FromMilliseconds(170)); move.BeginAnimation(Vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty, animation); await Task.Delay(175); }
        settings.Expanded = !settings.Expanded; canvas.RenderTransform = null; Render(); if (settings.Expanded) { canvas.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))); }
        animating = false; Save();
    }
    void Save() { if (Testing) return; try { settings.Save(); } catch (Exception ex) { ToolTip = "Não foi possível salvar posição: " + ex.Message; } }
    void Configure() { if (busy || Active) { MessageBox.Show("Finalize a gravação antes de alterar configurações.", "Eagle Eyes"); return; } collapseTimer.Stop(); new SettingsWindow(settings) { Owner = this }.ShowDialog(); Render(); }
    async Task Start()
    {
        if (busy || Active) return; busy = true; collapseTimer.Stop();
        try
        {
            if (!hotkeys && settings.HideDuringCapture) throw new InvalidOperationException("Os atalhos globais estão em uso por outro aplicativo. Libere Ctrl+Shift+P e Ctrl+Shift+F e reinicie, ou desmarque ocultar a barra nas configurações.");
            var ffmpeg = settings.ResolveBinary("ffmpeg.exe"); var ffprobe = settings.ResolveBinary("ffprobe.exe");
            CaptureArea? area;
            if (settings.FullMonitor) { var m = Native.Monitors().FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? Native.Monitors()[0]; area = new CaptureArea(m.Bounds.Left, m.Bounds.Top, m.Bounds.Width, m.Bounds.Height); }
            else { Hide(); area = await RegionSelection.SelectAsync(); Show(); }
            if (area == null) return;
            if (settings.HideDuringCapture) MessageBox.Show("Durante a gravação a barra ficará oculta para evitar que apareça no vídeo.\n\nCtrl+Shift+P: pausar/retomar\nCtrl+Shift+F: finalizar\n\nAo pausar, a barra reaparece.", "Eagle Eyes");
            session?.Dispose(); session = new RecordingSession(ffmpeg, ffprobe); settings.Expanded = true;
            for (int i = 3; i > 0; i--) { transient = i.ToString(); Render(); await Task.Delay(1000); }
            transient = ""; if (settings.HideDuringCapture) { Hide(); await Task.Delay(200); }
            await session.StartAsync(area, settings.OutputDirectory);
        }
        catch (Exception ex) { Show(); if (session?.State == RecordingState.Faulted) ReleaseFault(ex); else Report(ex); }
        finally { busy = false; transient = ""; Render(); }
    }
    async Task PauseResume()
    {
        if (busy || session == null) return; if (session.State is not (RecordingState.Recording or RecordingState.Paused)) return; busy = true;
        try
        {
            if (session.State == RecordingState.Recording) { await session.PauseAsync(); Show(); settings.Expanded = true; }
            else { if (settings.HideDuringCapture) { Hide(); await Task.Delay(200); } await session.ResumeAsync(); }
        }
        catch (Exception ex) { Show(); if (session?.State == RecordingState.Faulted) ReleaseFault(ex); else Report(ex); }
        finally { busy = false; Render(); }
    }
    async Task Stop()
    {
        if (busy || session == null || session.State is not (RecordingState.Recording or RecordingState.Paused)) return; busy = true; transient = "MP4…"; settings.Expanded = true;
        try
        {
            if (session.State == RecordingState.Recording) await session.PauseAsync();
            Show(); Render(); var output = await session.StopAsync(); session.Dispose(); session = null;
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + output + "\"", UseShellExecute = true });
        }
        catch (Exception ex) { Show(); ReleaseFault(ex); }
        finally { busy = false; transient = ""; Render(); }
    }
    void ReleaseFault(Exception ex) { var recovery = session?.RecoveryDirectory; session?.Dispose(); session = null; Report(ex, recovery); }
    void Report(Exception ex, string? recovery = null) { recovery ??= session?.RecoveryDirectory; MessageBox.Show(ex.Message + (string.IsNullOrEmpty(recovery) ? "" : "\n\nTemporários preservados em:\n" + recovery), "Eagle Eyes", MessageBoxButton.OK, MessageBoxImage.Error); }
    string DurationText() { var value = session?.RecordedDuration ?? TimeSpan.Zero; return value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss"); }
    async Task SmokeCycle() { var report = ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-smoke.json"); WriteSmoke(report); foreach (var edge in new[] { "Right", "Left", "Top", "Bottom" }) foreach (var expanded in new[] { true, false }) { settings.Edge = edge; settings.Expanded = expanded; Render(); await Task.Delay(150); WriteSmoke(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(report))!, System.IO.Path.GetFileNameWithoutExtension(report) + "-" + edge + "-" + (expanded ? "expanded" : "collapsed") + ".json")); } }
    void WriteSmoke(string? report = null)
    {
        Native.GetWindowRect(Handle, out var rect);
        var path = report ?? ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-smoke.json"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using (var stream = File.Create(System.IO.Path.ChangeExtension(path, ".png"))) encoder.Save(stream);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { windowCreated = Handle != IntPtr.Zero, hotkeysRegistered = hotkeys, edge = settings.Edge, expanded = settings.Expanded, transparentCornerIgnoresHit = canvas.InputHitTest(settings.Expanded ? (settings.Edge == "Left" ? new Point(Width - 1, 0) : settings.Edge == "Top" ? new Point(0, Height - 1) : new Point(0, 0)) : new Point(0, 0)) == null, centerAcceptsHit = canvas.InputHitTest(new Point(Width / 2, Height / 2)) != null, rect.Left, rect.Top, rect.Right, rect.Bottom, monitor = monitor.Device, visibleWithinWorkArea = rect.Left >= monitor.Work.Left && rect.Top >= monitor.Work.Top && rect.Right <= monitor.Work.Right && rect.Bottom <= monitor.Work.Bottom, scale = Native.GetDpiForWindow(Handle) }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    string? ArgumentValue(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    async Task RunProof()
    {
        try
        {
            settings.HideDuringCapture = true; settings.Expanded = true; session = new RecordingSession(settings.ResolveBinary("ffmpeg.exe"), settings.ResolveBinary("ffprobe.exe")); var m = monitor.Bounds; var area = new CaptureArea(m.Left + 40, m.Top + 40, Math.Min(640, m.Width - 80), Math.Min(360, m.Height - 80));
            Hide(); await Task.Delay(200); await session.StartAsync(area, ArgumentValue("--output") ?? settings.OutputDirectory); Render(); await Task.Delay(1800); await PauseResume(); await Task.Delay(800); await PauseResume(); await Task.Delay(1800); await PauseResume(); await Task.Delay(800); await PauseResume(); await Task.Delay(1800);
            busy = true; var output = await session.StopAsync(); var report = ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-proof.txt"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(report))!); File.WriteAllText(report, output + "\n" + session.RecordedDuration); session.Dispose(); session = null; busy = false; closing = true; Close();
        }
        catch (Exception ex) { File.WriteAllText(ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-proof.txt"), ex.ToString()); closing = true; Close(); }
    }
}
