using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--background")) { RunBackground(args); return; }
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var artifacts = Path.Combine(root, ".test-artifacts", "affinity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifacts);
        var application = new Application();
        var transparent = args.Contains("--transparent");
        var window = new Window { AllowsTransparency = transparent, Title = "Eagle Eyes affinity technical proof", Left = 100, Top = 100, Width = 240, Height = 180, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Topmost = true, Background = Brushes.Magenta, ShowInTaskbar = false };
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(800);
                var hwnd = new WindowInteropHelper(window).Handle;
                GetWindowRect(hwnd, out var rect);
                await Capture("before.png", rect);
                var affinity = SetWindowDisplayAffinity(hwnd, 0x11);
                var error = Marshal.GetLastWin32Error();
                await Task.Delay(500);
                await Capture("excluded.png", rect);
                window.Hide();
                await Task.Delay(500);
                await Capture("hidden.png", rect);
                var before = Magenta("before.png"); var excluded = Magenta("excluded.png"); var hidden = Magenta("hidden.png");
                var result = new { transparent, affinity, error, rect.Left, rect.Top, before, excluded, hidden, ExclusionObserved = before > 3000 && excluded < 100, HiddenObserved = before > 3000 && hidden < 100 };
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(artifacts, "result.json"), json);
                Console.WriteLine(json); Console.WriteLine("ARTIFACT " + artifacts);
                Environment.ExitCode = before > 3000 ? 0 : 1;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { application.Shutdown(); }
        };
        application.Run(window);
        async Task Capture(string file, Rect rect)
        {
            var ffmpeg = Path.Combine(root, "components", "ffmpeg-master-latest-win64-gpl", "bin", "ffmpeg.exe");
            var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-hide_banner", "-nostdin", "-n", "-f", "gdigrab", "-framerate", "15", "-draw_mouse", "0", "-offset_x", (rect.Left + 40).ToString(), "-offset_y", (rect.Top + 40).ToString(), "-video_size", "64x64", "-i", "desktop", "-frames:v", "1", "-update", "1", Path.Combine(artifacts, file) }) info.ArgumentList.Add(arg);
            using var p = Process.Start(info)!; var err = p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (p.ExitCode != 0) throw new Exception(await err);
        }
        int Magenta(string name)
        {
            using var stream = File.OpenRead(Path.Combine(artifacts, name));
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0); var pixels = new byte[64 * 64 * 4]; bitmap.CopyPixels(pixels, 64 * 4, 0);
            var count = 0; for (var i = 0; i < pixels.Length; i += 4) if (pixels[i] > 200 && pixels[i + 1] < 60 && pixels[i + 2] > 200) count++; return count;
        }
    }
    static void RunBackground(string[] args)
    {
        string Value(string name) { var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:throw new ArgumentException(name); }
        var values=Value("--bounds").Split(',').Select(int.Parse).ToArray();
        if(values.Length!=4||values[2]<=0||values[3]<=0)throw new ArgumentException("Bounds must be physical x,y,width,height");
        var application=new Application();
        var window=new Window{Title="Eagle Eyes QA magenta background",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=Brushes.Magenta,Left=values[0],Top=values[1],Width=values[2],Height=values[3]};
        window.Loaded+=(_,_)=>{
            var hwnd=new WindowInteropHelper(window).Handle;
            SetWindowPos(hwnd,new IntPtr(-1),values[0],values[1],values[2],values[3],0x10);
            GetWindowRect(hwnd,out var rect);
            File.WriteAllText(Value("--ready"),JsonSerializer.Serialize(new{hwnd=hwnd.ToInt64(),rect.Left,rect.Top,rect.Right,rect.Bottom}));
        };
        application.Run(window);
    }}


