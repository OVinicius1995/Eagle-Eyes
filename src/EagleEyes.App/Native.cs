using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace EagleEyes.App;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MONITORINFOEX { public int Size; public RECT Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    internal record Monitor(string Device, RECT Bounds, RECT Work);
    delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    internal static List<Monitor> Monitors() { var result = new List<Monitor>(); EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr d, ref RECT r, IntPtr p) => { var i = new MONITORINFOEX { Size = Marshal.SizeOf<MONITORINFOEX>(), Device = "" }; if (GetMonitorInfo(h, ref i)) result.Add(new(i.Device, i.Monitor, i.Work)); return true; }, IntPtr.Zero); return result; }
    internal static void Place(Window window, RECT bounds) { var h = new WindowInteropHelper(window).EnsureHandle(); SetWindowPos(h, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x14); }
    internal static RECT Rectangle(int x, int y, int w, int h) => new() { Left = x, Top = y, Right = x + w, Bottom = y + h };

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr window, int index, int value);
    internal static bool TryExcludeFromCapture(Window window)
    {
        try { return SetWindowDisplayAffinity(new WindowInteropHelper(window).EnsureHandle(), 0x11); }
        catch (InvalidOperationException) { return false; }
    }
    internal static void MakeClickThrough(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000020);
    }
    internal static void ClampToWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        if (!GetWindowRect(handle, out var current)) return;
        var monitors = Monitors(); if (monitors.Count == 0) return;
        long Intersection(RECT work)
        {
            long width = Math.Max(0, Math.Min(current.Right, work.Right) - Math.Max(current.Left, work.Left));
            long height = Math.Max(0, Math.Min(current.Bottom, work.Bottom) - Math.Max(current.Top, work.Top));
            return width * height;
        }
        double Distance(RECT work)
        {
            double x = ((double)current.Left + current.Right) / 2, y = ((double)current.Top + current.Bottom) / 2;
            double dx = Math.Max(work.Left - x, Math.Max(0, x - work.Right)), dy = Math.Max(work.Top - y, Math.Max(0, y - work.Bottom));
            return dx * dx + dy * dy;
        }
        var work = monitors.OrderByDescending(m => Intersection(m.Work)).ThenBy(m => Distance(m.Work)).First().Work;
        int width = Math.Min(current.Width, work.Width), height = Math.Min(current.Height, work.Height);
        int x = Math.Clamp(current.Left, work.Left, work.Right - width), y = Math.Clamp(current.Top, work.Top, work.Bottom - height);
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x14);
    }
}
