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
    CaptureArea? selectedArea; bool sessionPositioned, captureExcluded;
    TextBlock? duration;
    AnnotationWindow? annotations;
    Color penColor = PenPalette.Red;
    bool screenshotPending, screenshotHotkey;
    PrintPreview? preview;
    Task? printWorkflow;
    DateTime printNoticeUntil;
    string printNotice = "";
    string PrintTip => screenshotHotkey ? "Tirar print (Ctrl+Shift+S)" : "Tirar print (atalho indisponível)";
    string PrintHint(string target) => screenshotHotkey
        ? $"Clique na câmera ou pressione Ctrl+Shift+S para tirar um print {target}."
        : $"Clique na câmera para tirar um print {target}. O atalho está indisponível.";

    Native.Monitor monitor = null!;
    bool Testing => args.Contains("--smoke-test") || args.Contains("--proof-recording") || args.Contains("--ux-test");
    bool Vertical => settings.Edge is "Left" or "Right";
    UpdateRelease? pendingUpdate;
    internal FileStream? VerifiedInstaller { get; private set; }
    bool Active => session?.State is RecordingState.Recording or RecordingState.Paused or RecordingState.Finalizing;
    IntPtr Handle => new WindowInteropHelper(this).Handle;
    internal MainWindow(string[] args)
    {
        this.args = args; Title = "Eagle Eyes"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; Content = canvas;
        var available = Native.Monitors(); if (available.Count == 0) throw new InvalidOperationException("Nenhum monitor disponível."); monitor = available.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? available[0]; settings.MonitorDevice = monitor.Device;
        if (settings.Edge is not ("Left" or "Right" or "Top" or "Bottom")) settings.Edge = "Right";
        SourceInitialized += (_, _) => { var source = HwndSource.FromHwnd(Handle); source?.AddHook(Hook); hotkeys = Native.RegisterHotKey(Handle, 1, 0x4006, 0x50); hotkeys = Native.RegisterHotKey(Handle, 2, 0x4006, 0x46) && hotkeys; screenshotHotkey = Native.RegisterHotKey(Handle, 3, 0x4006, 0x53); captureExcluded = TryExcludeControls(); };
        Loaded += async (_, _) => { Render(); Dock(); if (args.Contains("--smoke-test")) { await Task.Delay(800); await SmokeCycle(); closing = true; Close(); } else if (args.Contains("--proof-recording")) await RunProof(); };
        Closing += async (_, e) =>
        {
            if (!closing && (busy || Active))
            {
                e.Cancel = true;
                if (!busy && Active) await Discard();
                return;
            }
            if (printWorkflow is { IsCompleted: false } pending)
            {
                e.Cancel = true;
                closing = true;
                preview?.FinishForExit();
                await pending;
                _ = Dispatcher.BeginInvoke(Close);
                return;
            }
            Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
            Native.UnregisterHotKey(Handle, 3); CloseAnnotations(); timer.Stop(); collapseTimer.Stop(); session?.Dispose();
            if (!Testing) Save();
        };
        timer.Tick += (_, _) =>
        {
            if (printNotice.Length > 0 && DateTime.UtcNow >= printNoticeUntil)
            {
                printNotice = "";
                if (session != null && !busy) Render();
            }
            if (duration != null) duration.Text = DurationText();
            if (!busy && session?.State == RecordingState.Faulted)
            {
                busy = true;
                ReleaseFault(new IOException("O processo de captura foi encerrado inesperadamente."), "capture");
                busy = false; Render();
            }
        };
        timer.Tick += (_, _) =>
        {
            if (pendingUpdate == null || !settings.CheckForUpdates || busy || Active || !IsVisible || OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) return;
            var release = pendingUpdate; pendingUpdate = null;
            OfferUpdate(this, release);
        };
        Loaded += async (_, _) =>
        {
            if (Testing || !settings.CheckForUpdates) return;
            try { await Task.Delay(15000); if (IsLoaded && settings.CheckForUpdates) pendingUpdate = await UpdateService.CheckAsync(UpdateService.CurrentVersion); }
            catch (Exception error) { AppLog.Write("update-background", error); }
        };
        timer.Start();
        collapseTimer.Tick += async (_, _) => { collapseTimer.Stop(); if (!settings.KeepExpanded && !IsMouseOver && !busy && !Active && settings.Expanded) await ToggleExpanded(); };
        WindowDragBehavior.Attach(this, SnapAfterDrag);
        MouseEnter += (_, _) => collapseTimer.Stop(); MouseLeave += (_, _) => { if (!settings.KeepExpanded) collapseTimer.Start(); };
    }
    internal void OfferUpdate(Window owner, UpdateRelease release)
    {
        if (!IsLoaded || busy || session != null || closing) return;
        busy = true;
        try
        {
            var installer = UserMessages.UpdateAvailable(owner, release);
            if (installer == null) return;
            VerifiedInstaller = installer;
            closing = true;
            if (owner != this) owner.Close();
            Close();
        }
        finally { busy = false; }
    }

    internal void ShowControls()
    {
        // Selection and countdown own the current interaction; do not restore an idle bar over them.
        if (busy) return;
        var dialog = OwnedWindows.Cast<Window>().LastOrDefault(window => window.IsVisible);
        if (dialog != null) { dialog.Activate(); return; }
        Show();
        if (session == null) { settings.Expanded = true; Render(); }
        else Native.ClampToWorkArea(this);
        Activate();
    }

    internal async Task ExitFromTrayAsync()
    {
        if (busy) return;
        if (Active)
        {
            await Discard();
            if (session != null) return;
        }
        if (busy || session != null) return;
        closing = true;
        Close();
    }

    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x312) { handled = true; if (w.ToInt32() == 1) _ = PauseResume(); else if (w.ToInt32() == 2) _ = Stop(); else if (w.ToInt32() == 3) _ = TakeScreenshot(); else if (w.ToInt32() == 4 && annotations != null) { CloseAnnotations(); Render(); } }
        if (msg == 0x47) annotations?.ExcludeToolbar(h); // WM_WINDOWPOSCHANGED
        if (msg == 0x2e0) Dispatcher.BeginInvoke(Dock);
        if (msg == 0x7e && !Active && !busy) Dispatcher.BeginInvoke(() => { var all = Native.Monitors(); if (all.Count > 0) { monitor = all.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? all[0]; settings.MonitorDevice = monitor.Device; Dock(); } });
        return IntPtr.Zero;
    }
    ToolTip CaptureToolTip(string text) => CaptureSafeToolTip.Create(text, !(Testing && args.Contains("--disable-capture-exclusion")));

    Button IconButton(string tip, FunctionalIcon icon, Action action, double size = 32, string? hint = null)
    {
        var button = new Button { Width = size, Height = size, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = ink, Cursor = Cursors.Hand, ToolTip = CaptureToolTip(tip), Padding = new Thickness(6), Focusable = true };
        var border = new FrameworkElementFactory(typeof(Border)); border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter); var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(45, 183, 185, 205)))); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(65, 183, 185, 205)))); template.Triggers.Add(focus); button.Template = template;
        if (hint != null) CaptureSafeToolTip.SetHint(button, hint);
        double visualSize = icon == FunctionalIcon.Settings || (icon == FunctionalIcon.Close && size <= 28) ? 14 : icon == FunctionalIcon.Video ? 22 : icon == FunctionalIcon.ChevronRight ? 18 : 20;
        button.Content = FunctionalIcons.Create(icon, visualSize);
        AutomationProperties.SetName(button, tip); button.Click += (_, _) => action(); return button;
    }
    void Render()
    {
        if (session != null) { RenderRecordingBar(); return; }
        canvas.Children.Clear(); ToolTip = null; bool expanded = settings.Expanded; double length = 164; double width = Vertical ? (expanded ? 100 : 38) : (expanded ? length : 58), height = Vertical ? (expanded ? length : 58) : (expanded ? 100 : 38); Width = width; Height = height;
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
        var arrow = IconButton(expanded ? "Recolher barra" : "Expandir barra", FunctionalIcon.ChevronRight, () => _ = ToggleExpanded(), 34);
        arrow.RenderTransformOrigin = new Point(.5, .5); double angle = settings.Edge switch { "Left" => 180, "Top" => -90, "Bottom" => 90, _ => 0 }; if (!expanded) angle += 180; arrow.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(arrow, Vertical ? (expanded ? (settings.Edge == "Right" ? 2 : 64) : 2) : (width - 34) / 2); Canvas.SetTop(arrow, Vertical ? (height - 34) / 2 : (expanded ? (settings.Edge == "Top" ? 64 : 2) : 2)); canvas.Children.Add(arrow);
        if (!expanded) { duration = null; Dock(); return; }
        var body = new StackPanel { Orientation = Vertical ? Orientation.Vertical : Orientation.Horizontal, Width = Vertical ? 62 : length, Height = Vertical ? length : 62, VerticalAlignment = VerticalAlignment.Center };
        Canvas.SetLeft(body, settings.Edge == "Right" ? 38 : 0); Canvas.SetTop(body, settings.Edge == "Bottom" ? 38 : 0); canvas.Children.Add(body);
        var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3) };
        top.Children.Add(IconButton("Configurações", FunctionalIcon.Settings, Configure, 28));
        top.Children.Add(IconButton("Fechar Eagle Eyes", FunctionalIcon.Close, Close, 28)); body.Children.Add(top);
        var drag = new Border { Width = Vertical ? 54 : 12, Height = Vertical ? 12 : 54, Background = Brushes.Transparent, Cursor = Cursors.SizeAll, ToolTip = CaptureToolTip("Arraste para mover e encaixar na borda"), Child = new Path { Data = Geometry.Parse(Vertical ? "M17,3 L35,3 M17,7 L35,7" : "M3,17 L3,35 M7,17 L7,35"), Stroke = ink, StrokeThickness = 1 } };
        body.Children.Add(drag);
        duration = null; body.Children.Add(IconButton("Gravar vídeo", FunctionalIcon.Video, () => _ = Start(), 42));
        body.Children.Add(IconButton(PrintTip, FunctionalIcon.Camera, () => _ = TakeScreenshot(), 36, PrintHint("do monitor selecionado")));
        Dock();
    }
    void Dock()
    {
        if (Handle == IntPtr.Zero) return; if (session != null) { Native.ClampToWorkArea(this); return; } var all = Native.Monitors(); if (all.Count == 0) return; monitor = all.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? all[0]; settings.MonitorDevice = monitor.Device;
        double scale = Math.Max(1, Native.GetDpiForWindow(Handle) / 96d); int width = (int)Math.Round(Width * scale), height = (int)Math.Round(Height * scale); var w = monitor.Work; double offset = double.IsFinite(settings.Offset) ? Math.Clamp(settings.Offset, 0, 1) : 0.5;
        int x = w.Left + (int)((w.Width - width) * offset), y = w.Top + (int)((w.Height - height) * offset);
        if (settings.Edge == "Right") x = w.Right - width; if (settings.Edge == "Left") x = w.Left; if (settings.Edge == "Top") y = w.Top; if (settings.Edge == "Bottom") y = w.Bottom - height;
        x = Math.Clamp(x, w.Left, Math.Max(w.Left, w.Right - width)); y = Math.Clamp(y, w.Top, Math.Max(w.Top, w.Bottom - height)); Native.Place(this, Native.Rectangle(x, y, width, height));
    }
    void SnapAfterDrag()
    {
        if (session != null) { Native.ClampToWorkArea(this); return; }
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
    void Save() { if (Testing) return; try { settings.Save(); } catch (Exception ex) { AppLog.Write("save-position", ex); ToolTip = CaptureToolTip("Não foi possível lembrar a posição da barra. Tente novamente."); } }
    void Configure() { if (busy || Active) { UserMessages.Show(this, "Salve ou descarte a gravação antes de alterar as configurações."); return; } collapseTimer.Stop(); new SettingsWindow(settings) { Owner = this }.ShowDialog(); if (!closing) Render(); }
    bool TryExcludeControls()
    {
        if (Testing && args.Contains("--disable-capture-exclusion")) return false;
        return Native.TryExcludeFromCapture(this);
    }

    async Task Start()
    {
        if (busy || Active || animating) return;
        busy = true; collapseTimer.Stop();
        PreparationWindow? preparation = null;
        try
        {
            var ffmpeg = settings.ResolveBinary("ffmpeg.exe");
            var ffprobe = settings.ResolveBinary("ffprobe.exe");
            CaptureArea? area = null;
            bool selectConfiguredMonitor = settings.FullMonitor;
            while (true)
            {
                Hide();
                if (selectConfiguredMonitor)
                {
                    var monitors = Native.Monitors();
                    var chosen = monitors.FirstOrDefault(m => m.Device == settings.MonitorDevice) ?? monitors.FirstOrDefault();
                    if (chosen == null) throw new InvalidOperationException("Nenhum monitor disponível.");
                    area = new CaptureArea(chosen.Bounds.Left, chosen.Bounds.Top, chosen.Bounds.Width, chosen.Bounds.Height);
                    selectConfiguredMonitor = false;
                }
                else area = await RegionSelection.SelectAsync();
                if (area == null) break;
                preparation = new PreparationWindow(area);
                bool reselect = false;
                while (true)
                {
                    var action = await preparation.NextActionAsync();
                    preparation.HideAll();
                    if (action == PreparationAction.Cancel) return;
                    if (action == PreparationAction.Reselect) { reselect = true; break; }
                    var monitors = Native.Monitors();
                    var chosen = monitors.FirstOrDefault(m => area.X >= m.Bounds.Left && area.Y >= m.Bounds.Top && (long)area.X + area.Width <= m.Bounds.Right && (long)area.Y + area.Height <= m.Bounds.Bottom);
                    if (chosen == null) { UserMessages.Show(preparation, "A tela selecionada não está mais disponível. Selecione outra área."); reselect = true; break; }
                    if (!await CountdownWindow.RunAsync(chosen)) continue;
                    preparation.Close(); preparation = null;
                    // Allow the compositor to remove countdown/outline before opening the capture input.
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Task.Delay(180);
                    selectedArea = area; sessionPositioned = false;
                    session?.Dispose(); session = new RecordingSession(ffmpeg, ffprobe);
                    transient = "Iniciando…"; Show(); Render();
                    captureExcluded = TryExcludeControls();
                    if (!captureExcluded && !Testing)
                        UserMessages.Show(this, "A barra continuará visível, mas poderá aparecer no vídeo neste Windows. Para evitar isso, posicione-a fora da área escolhida.");
                    await Task.Delay(150);
                    await session.StartAsync(area, settings.OutputDirectory);
                    if (!hotkeys) AppLog.Write("hotkeys", details: "Atalhos indisponíveis. Os botões permanecem acessíveis.");
                    return;
                }
                preparation.Close(); preparation = null;
                if (!reselect) break;
            }
        }
        catch (Exception ex)
        {
            preparation?.Close(); preparation = null;
            Show(); ReleaseFault(ex, "start");
        }
        finally
        {
            preparation?.Close(); busy = false; transient = ""; Show(); Render();
        }
    }

    void RenderRecordingBar()
    {
        canvas.Children.Clear(); Width = 580; Height = 60;
        canvas.Clip = new RectangleGeometry(new Rect(0, 0, Width, Height), 10, 10);
        canvas.Children.Add(new Path { Data = canvas.Clip, Fill = dark });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 60, Margin = new Thickness(12, 0, 12, 0) };
        canvas.Children.Add(row);
        var paused = session?.State == RecordingState.Paused;
        row.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = paused ? Brushes.Goldenrod : Brushes.OrangeRed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
        var status = new TextBlock { Text = transient.Length > 0 ? transient : printNotice.Length > 0 ? printNotice : paused ? "Pausado" : "Gravando", Foreground = ink, FontSize = 12, Width = 154, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(status, "RecordingStatus"); row.Children.Add(status);
        duration = new TextBlock { Text = DurationText(), Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 14, Width = 78, VerticalAlignment = VerticalAlignment.Center, ToolTip = CaptureToolTip("Tempo efetivamente gravado") };
        AutomationProperties.SetAutomationId(duration, "RecordingDuration"); row.Children.Add(duration);
        void Separator() => row.Children.Add(new Border { Width = 1, Height = 24, Background = new SolidColorBrush(Color.FromRgb(66, 67, 85)), Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
        Separator();
        var pause = IconButton(paused ? "Retomar gravação" : "Pausar gravação", paused ? FunctionalIcon.Play : FunctionalIcon.Pause, () => _ = PauseResume(), 36);
        pause.IsEnabled = !busy; row.Children.Add(pause);
        row.Children.Add(IconButton(PrintTip, FunctionalIcon.Camera, () => _ = TakeScreenshot(), 32, PrintHint("da área da gravação")));
        row.Children.Add(IconButton(annotations == null ? "Ativar lápis" : "Desativar lápis", FunctionalIcon.Pencil, ToggleAnnotations, 32, PenPalette.PencilHint(annotations != null)));
        foreach (var (name, color) in PenPalette.ColorsByName)
        {
            var swatch = IconButton("Lápis " + name.ToLowerInvariant(), FunctionalIcon.Pencil, () => ChooseColor(color), 22, PenPalette.ColorHint(name));
            swatch.Content = PenPalette.Swatch(color, annotations != null && color == penColor);
            row.Children.Add(swatch);
        }
        var save = IconButton("Parar e salvar", FunctionalIcon.Stop, () => _ = Stop(), 36);
        save.IsEnabled = !busy; row.Children.Add(save);
        Separator();
        var discard = IconButton("Descartar gravação", FunctionalIcon.Close, () => _ = Discard(), 36);
        discard.IsEnabled = !busy; row.Children.Add(discard);
        ToolTip = CaptureToolTip(paused ? "Gravação pausada. Continue quando estiver pronto." : "Ctrl+Shift+P: pausar/retomar · Ctrl+Shift+F: parar e salvar");
        if (!sessionPositioned) PlaceRecordingBar();
        else Native.ClampToWorkArea(this);
    }

    void PlaceRecordingBar()
    {
        if (Handle == IntPtr.Zero || selectedArea == null) return;
        var area = selectedArea;
        var all = Native.Monitors();
        var target = all.FirstOrDefault(m => area.X >= m.Bounds.Left && area.X < m.Bounds.Right && area.Y >= m.Bounds.Top && area.Y < m.Bounds.Bottom) ?? all.FirstOrDefault();
        if (target == null) return;
        double scale = Math.Max(1, Native.GetDpiForWindow(Handle) / 96d);
        int width = (int)Math.Ceiling(Width * scale), height = (int)Math.Ceiling(Height * scale);
        int x = area.X + Math.Max(0, (area.Width - width) / 2), y = area.Y - height - 12;
        if (y < target.Work.Top) y = area.Y + area.Height + 12;
        if (y + height > target.Work.Bottom) y = target.Work.Top + 12;
        Native.Place(this, Native.Rectangle(x, y, width, height));
        Native.ClampToWorkArea(this); sessionPositioned = true;
    }

    Task TakeScreenshot()
    {
        if (printWorkflow is { IsCompleted: false })
        {
            preview?.Activate();
            return printWorkflow;
        }
        return printWorkflow = ProcessScreenshot();
    }

    async Task ProcessScreenshot()
    {
        if (busy || closing || screenshotPending || !IsEnabled || !IsVisible) return;
        var bounds = monitor.Bounds;
        var area = selectedArea ?? new CaptureArea(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        var directory = settings.OutputDirectory;
        screenshotPending = true;
        try
        {
            var image = await Task.Run(() => Screenshot.Capture(area));
            var work = (Native.Monitors().FirstOrDefault(m => area.X >= m.Bounds.Left && area.X < m.Bounds.Right && area.Y >= m.Bounds.Top && area.Y < m.Bounds.Bottom) ?? monitor).Work;
            preview = new PrintPreview(image, area, work) { Owner = this };
            preview.Show();
            if (closing) preview.FinishForExit();
            bool copied = await CopyPrint(image);
            var final = await preview.Result;
            preview = null;
            if (!ReferenceEquals(final, image) || !copied) copied = await CopyPrint(final);
            await Task.Run(() => Screenshot.Save(final, directory));
            if (!copied)
            {
                UserMessages.Show(this, "Print salvo na pasta de saída. A área de transferência do Windows está ocupada.");
                return;
            }
            if (session != null && !busy) { ShowPrintNotice("Print salvo"); }
        }
        catch (Exception ex)
        {
            AppLog.Write("screenshot", ex);
            if (session != null && !busy) { ShowPrintNotice("Falha ao salvar print"); }
            else if (IsVisible && !busy) UserMessages.Show(this, "Não foi possível salvar o print.", ex.Message);
        }
        finally { preview = null; screenshotPending = false; }
    }

    static async Task<bool> CopyPrint(System.Windows.Media.Imaging.BitmapSource image)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try { Clipboard.SetImage(image); return true; }
            catch (System.Runtime.InteropServices.ExternalException) { await Task.Delay(50); }
        }
        return false;
    }

    void ShowPrintNotice(string message)
    {
        printNotice = message;
        printNoticeUntil = DateTime.UtcNow.AddSeconds(2);
        Render();
    }

    void ChooseColor(Color color)
    {
        if (busy || selectedArea == null) return;
        penColor = color;
        if (annotations == null) ToggleAnnotations();
        else { annotations.SetColor(color); Render(); }
    }

    void ToggleAnnotations()
    {
        if (busy || selectedArea == null) return;
        if (annotations != null) CloseAnnotations();
        else
        {
            annotations = new AnnotationWindow(selectedArea, penColor);
            annotations.Show();
            // Windows keeps an owned toolbar above its drawing surface, including after moving or clicking.
            Owner = annotations;
            annotations.ExcludeToolbar(Handle);
            Native.RegisterHotKey(Handle, 4, 0x4000, 0x1B);
        }
        Render();
    }

    void CloseAnnotations()
    {
        if (annotations == null) return;
        Native.UnregisterHotKey(Handle, 4);
        Owner = null;
        annotations.Close();
        annotations = null;
    }

    async Task PauseResume()
    {
        if (busy || session?.State is not (RecordingState.Recording or RecordingState.Paused)) return;
        busy = true; Render();
        try
        {
            if (session.State == RecordingState.Recording) await session.PauseAsync();
            else await session.ResumeAsync();
        }
        catch (Exception ex) { ReleaseFault(ex, "pause-resume"); }
        finally { busy = false; transient = ""; Show(); Render(); }
    }

    async Task Stop()
    {
        if (busy || session?.State is not (RecordingState.Recording or RecordingState.Paused)) return;
        busy = true; transient = "Salvando seu vídeo…"; Render();
        try
        {
            var output = await session.StopAsync();
            var remainingDirectory = session.RecoveryDirectory;
            CloseAnnotations(); session.Dispose(); session = null; selectedArea = null; transient = "";
            Render();
            UserMessages.Saved(this, output, remainingDirectory);
        }
        catch (Exception ex) { ReleaseFault(ex, "save"); }
        finally { busy = false; transient = ""; Show(); Render(); }
    }

    async Task Discard()
    {
        if (busy || session?.State is not (RecordingState.Recording or RecordingState.Paused)) return;
        busy = true;
        CloseAnnotations();
        bool wasRecording = session.State == RecordingState.Recording;
        try
        {
            // Pause and close the segment before any modal can be composed into the recording.
            if (wasRecording) await session.PauseAsync();
            Render();
            if (!UserMessages.ConfirmDiscard(this))
            {
                // The confirmation is fully closed before resuming the capture process.
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(150);
                if (wasRecording) await session.ResumeAsync();
                return;
            }
            transient = "Descartando…"; Render();
            var result = await session.DiscardAsync();
            CloseAnnotations(); session.Dispose(); session = null; selectedArea = null;
            if (!result.Complete)
            {
                var details = string.Join(Environment.NewLine, result.RemainingPaths);
                AppLog.Write("discard-partial", details: details);
                UserMessages.Show(this, "Não foi possível apagar todos os arquivos desta gravação. Alguns arquivos permaneceram na pasta de saída.", details);
            }
        }
        catch (Exception ex) { ReleaseFault(ex, "discard"); }
        finally { busy = false; transient = ""; Show(); Render(); }
    }

    void ReleaseFault(Exception error, string operation)
    {
        var recovery = session?.RecoveryDirectory;
        CloseAnnotations(); session?.Dispose(); session = null; selectedArea = null;
        AppLog.Write(operation, error, recovery);
        bool preserved = false;
        try { preserved = recovery != null && Directory.Exists(recovery) && Directory.EnumerateFiles(recovery, "*.mp4").Any(p => new FileInfo(p).Length > 0); } catch { }
        string message = operation == "start"
            ? error is FileNotFoundException ? error.Message : error is UnauthorizedAccessException or DirectoryNotFoundException ? "Não conseguimos salvar nesta pasta. Escolha outra pasta para continuar." : "Não foi possível iniciar a gravação. Confira as configurações e tente novamente."
            : operation == "save" && preserved ? "Não foi possível concluir o vídeo. Os trechos gravados foram preservados para recuperação."
            : operation == "discard" ? "Não foi possível descartar todos os arquivos. Confira a pasta de saída antes de tentar novamente."
            : "A gravação foi interrompida. Confira os detalhes e tente novamente.";
        string details = error.Message + (preserved ? "\n\nTrechos disponíveis em:\n" + recovery : "") + "\n\nLogs: " + AppLog.DirectoryPath;
        Show(); Render(); UserMessages.Show(this, message, details);
    }

    string DurationText()
    {
        var value = session?.RecordedDuration ?? TimeSpan.Zero;
        return $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
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
            selectedArea = area; sessionPositioned = false; Show(); Render(); await session.StartAsync(area, ArgumentValue("--output") ?? settings.OutputDirectory); Render(); await Task.Delay(1800); await PauseResume(); await Task.Delay(800); await PauseResume(); await Task.Delay(1800); await PauseResume(); await Task.Delay(800); await PauseResume(); await Task.Delay(1800);
            busy = true; var output = await session.StopAsync(); var report = ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-proof.txt"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(report))!); File.WriteAllText(report, output + "\n" + session.RecordedDuration); session.Dispose(); session = null; busy = false; closing = true; Close();
        }
        catch (Exception ex) { File.WriteAllText(ArgumentValue("--report") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eagle-eyes-ui-proof.txt"), ex.ToString()); closing = true; Close(); }
    }
}

