using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EagleEyes.Core;

internal static class CursorProof
{
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd, ref Point point);

    internal static void Run(string[] args)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var directory = Path.Combine(root, ".test-artifacts", "cursor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int engineOption = Array.IndexOf(args, "--engine-dir");
        var bin = engineOption >= 0 ? Path.GetFullPath(args[engineOption + 1]) : Path.Combine(root, "components", "ffmpeg-master-latest-win64-gpl", "bin");
        var ffmpeg = Path.Combine(bin, "ffmpeg.exe");
        var app = new Application();
        var window = new Window { Left = 100, Top = 100, Width = 320, Height = 220, Background = Brushes.Magenta,
            Cursor = Cursors.Arrow, Topmost = true, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize };
        window.Loaded += async (_, _) =>
        {
            if (!GetCursorPos(out var previous)) { app.Shutdown(1); Environment.ExitCode = 1; return; }
            using var session = new RecordingSession(ffmpeg, Path.Combine(bin, "ffprobe.exe"));
            try
            {
                var start = new Point { X = 32, Y = 32 };
                if (!ClientToScreen(new WindowInteropHelper(window).Handle, ref start)) throw new IOException("ClientToScreen failed");
                var area = new CaptureArea(start.X, start.Y, 160, 96);
                SetCursorPos(start.X + 10, start.Y + 32);
                await Task.Delay(500);
                await session.StartAsync(area, directory);
                for (int i = 0; i < 45; i++)
                {
                    SetCursorPos(start.X + 10 + i * 2, start.Y + 32);
                    await Task.Delay(70);
                }
                await Task.Delay(700); // Also inspect frames after motion stops.
                await session.PauseAsync();
                if (!Directory.EnumerateFiles(session.RecoveryDirectory!, "*-dxgi.mp4").Any(p => new FileInfo(p).Length > 0))
                    throw new IOException("Expected Desktop Duplication recording");
                string video = await session.StopAsync();
                var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                foreach (var arg in new[] { "-v", "error", "-i", video, "-pix_fmt", "rgb24", "-f", "rawvideo", "pipe:1" }) info.ArgumentList.Add(arg);
                using var decoder = Process.Start(info)!;
                byte[] frame = new byte[area.Width * area.Height * 3];
                int total = 0, visible = 0, firstVisible = -1, left = int.MaxValue, right = int.MinValue;
                var stream = decoder.StandardOutput.BaseStream;
                while (await stream.ReadAtLeastAsync(frame, frame.Length, false) == frame.Length)
                {
                    int cursorPixels = 0, magenta = 0, minX = area.Width;
                    for (int p = 0; p < frame.Length; p += 3)
                    {
                        int r = frame[p], g = frame[p + 1], b = frame[p + 2];
                        if (r > 180 && g < 70 && b > 180) magenta++;
                        int y = p / 3 / area.Width;
                        if (y >= 25 && y < 80 && Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 40)
                        { cursorPixels++; minX = Math.Min(minX, p / 3 % area.Width); }
                    }
                    if (magenta < area.Width * area.Height * .85) throw new IOException("Unexpected test background");
                    if (cursorPixels >= 12) { if (firstVisible < 0) firstVisible = total; visible++; left = Math.Min(left, minX); right = Math.Max(right, minX); }
                    total++;
                }
                await decoder.WaitForExitAsync();
                string result = $"Cursor visible in {visible}/{total} frames; first visible at frame {firstVisible}; horizontal travel {right - left}px. Video: {video}";
                File.WriteAllText(Path.Combine(directory, "result.txt"), result);
                Console.WriteLine(result);
                if (decoder.ExitCode != 0 || visible < 45 || firstVisible > 15 || firstVisible + visible != total || right - left < 65)
                    throw new IOException("Cursor visibility or movement check failed");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { SetCursorPos(previous.X, previous.Y); app.Shutdown(); }
        };
        app.Run(window);
    }
}