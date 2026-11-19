using System.Runtime.InteropServices;

namespace EagleEyes.Core;

// FFmpeg identifies a DXGI output by adapter and output index. Windows monitor
// enumeration uses a different ordering, so match the physical desktop rectangle.
internal sealed record DesktopDuplication(int Adapter, int Output, int OffsetX, int OffsetY)
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OutputDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public int Left, Top, Right, Bottom, Attached, Rotation;
        public IntPtr Monitor;
    }

    [DllImport("dxgi.dll")]
    static extern int CreateDXGIFactory(in Guid iid, out IntPtr factory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int Enumerate(IntPtr instance, int index, out IntPtr item);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int GetDescription(IntPtr output, out OutputDescription description);

    // IDXGIFactory.EnumAdapters, IDXGIAdapter.EnumOutputs and IDXGIOutput.GetDesc
    // each immediately follow the seven inherited IUnknown/IDXGIObject methods.
    static T FirstDxgiMethod<T>(IntPtr instance) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), 7 * IntPtr.Size));

    internal static DesktopDuplication? Find(CaptureArea area)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var iid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        if (CreateDXGIFactory(iid, out var factory) < 0) return null;
        try
        {
            var adapters = FirstDxgiMethod<Enumerate>(factory);
            for (int a = 0; adapters(factory, a, out var adapter) >= 0; a++)
            {
                try
                {
                    var outputs = FirstDxgiMethod<Enumerate>(adapter);
                    for (int o = 0; outputs(adapter, o, out var output) >= 0; o++)
                    {
                        try
                        {
                            if (FirstDxgiMethod<GetDescription>(output)(output, out var d) >= 0 &&
                                d.Attached != 0 && d.Rotation == 1 && area.X >= d.Left && area.Y >= d.Top &&
                                (long)area.X + area.Width <= d.Right && (long)area.Y + area.Height <= d.Bottom)
                                return new(a, o, area.X - d.Left, area.Y - d.Top);
                        }
                        finally { Marshal.Release(output); }
                    }
                }
                finally { Marshal.Release(adapter); }
            }
        }
        finally { Marshal.Release(factory); }
        return null;
    }
}