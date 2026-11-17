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
}
