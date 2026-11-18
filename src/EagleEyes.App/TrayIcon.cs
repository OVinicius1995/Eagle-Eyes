using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace EagleEyes.App;

/// <summary>Native notification icon; no additional framework or package dependency.</summary>
internal sealed class TrayIcon : IDisposable
{
    const int CallbackMessage = 0x8000 + 86;
    readonly MainWindow window;
    readonly uint taskbarCreated;
    readonly ContextMenu menu;
    HwndSource? source;
    IntPtr icon;
    NotifyIconData data;
    bool disposed;
    internal bool IsRegistered { get; private set; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id, Flags, Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW", SetLastError = true)]
    static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string message);

    internal TrayIcon(MainWindow window)
    {
        this.window = window;
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        menu = new ContextMenu { Placement = PlacementMode.MousePoint, Opacity = 0 };
        var show = new MenuItem { Header = "Mostrar controles" }; show.Click += (_, _) => window.ShowControls();
        var exit = new MenuItem { Header = "Sair" }; exit.Click += async (_, _) => await window.ExitFromTrayAsync();
        menu.Items.Add(show); menu.Items.Add(new Separator()); menu.Items.Add(exit);
        menu.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource popup) Native.SetWindowDisplayAffinity(popup.Handle, 0x11);
            menu.Opacity = 1;
        };
        menu.Closed += (_, _) => menu.Opacity = 0;
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Attach();
    }
    void OnSourceInitialized(object? sender, EventArgs args) => Attach();
    void OnClosed(object? sender, EventArgs args) => Dispose();
    void Attach()
    {
        if (source != null || disposed) return;
        var handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle);
        if (source == null) return;
        source.AddHook(Hook);
        icon = LoadImage(IntPtr.Zero, Branding.IconFilePath, 1, Math.Max(16, GetSystemMetrics(49)), Math.Max(16, GetSystemMetrics(50)), 0x10);
        if (icon == IntPtr.Zero) { AppLog.Write("tray-icon", details: "Unable to load official ICO: " + Marshal.GetLastWin32Error()); return; }
        data = new NotifyIconData { Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = handle, Id = 1, Flags = 1 | 2 | 4, Callback = CallbackMessage, Icon = icon, Tip = "Eagle Eyes", Info = "", InfoTitle = "" };
        IsRegistered = ShellNotifyIcon(0, ref data);
        if (!IsRegistered) AppLog.Write("tray-register", details: "Shell notification icon unavailable: " + Marshal.GetLastWin32Error());
    }
    IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)message == taskbarCreated && icon != IntPtr.Zero)
        { IsRegistered = ShellNotifyIcon(0, ref data); return IntPtr.Zero; }
        if (message != CallbackMessage) return IntPtr.Zero;
        handled = true;
        switch (lParam.ToInt32())
        {
            case 0x0203: window.ShowControls(); break; // Double click.
            case 0x0205: menu.IsOpen = true; break; // Right-button release.
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; menu.IsOpen = false;
        window.SourceInitialized -= OnSourceInitialized; window.Closed -= OnClosed;
        if (IsRegistered) ShellNotifyIcon(2, ref data);
        IsRegistered = false;
        source?.RemoveHook(Hook); source = null;
        if (icon != IntPtr.Zero) { DestroyIcon(icon); icon = IntPtr.Zero; }
    }
}