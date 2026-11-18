using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class EagleEyesBrandAssetTool
{
    static int Min(int color) { return Math.Min((color >> 16) & 255, Math.Min((color >> 8) & 255, color & 255)); }
    static int Clamp(double value) { return Math.Max(0, Math.Min(255, (int)Math.Round(value))); }
    public static int[] Prepare(string source, string assets, string previews)
    {
        Directory.CreateDirectory(assets); Directory.CreateDirectory(previews);
        using (Bitmap input = new Bitmap(source))
        {
            int w = input.Width, h = input.Height, count = w * h;
            int[] original = new int[count]; bool[] dark = new bool[count], seed = new bool[count];
            int[] integral = new int[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x; original[i] = input.GetPixel(x, y).ToArgb(); dark[i] = Min(original[i]) < 190;
                integral[(y + 1) * (w + 1) + x + 1] = (dark[i] ? 1 : 0) + integral[y * (w + 1) + x + 1] + integral[(y + 1) * (w + 1) + x] - integral[y * (w + 1) + x];
            }
            // Erode only to identify uncontaminated color references. The output outline is not eroded.
            for (int y = 4; y < h - 4; y++) for (int x = 4; x < w - 4; x++)
            {
                int l = x - 4, t = y - 4, r = x + 5, b = y + 5;
                int total = integral[b * (w + 1) + r] - integral[t * (w + 1) + r] - integral[b * (w + 1) + l] + integral[t * (w + 1) + l];
                seed[y * w + x] = total == 81;
            }
            int[] rgba = new int[count];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x, c = original[i];
                if (255 - Min(c) <= 12) continue; // White matte and near-white background texture, including eye interiors.
                if (seed[i]) { rgba[i] = c; continue; }
                int nearest = -1, distance = int.MaxValue;
                for (int radius = 4; radius <= 32 && nearest < 0; radius *= 2)
                {
                    for (int yy = Math.Max(0, y - radius); yy <= Math.Min(h - 1, y + radius); yy++)
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(w - 1, x + radius); xx++)
                    {
                        int candidate = yy * w + xx; if (!seed[candidate]) continue;
                        int d = (xx - x) * (xx - x) + (yy - y) * (yy - y);
                        if (d < distance) { distance = d; nearest = candidate; }
                    }
                }
                if (nearest < 0) continue;
                int f = original[nearest];
                double fr = (f >> 16) & 255, fg = (f >> 8) & 255, fb = f & 255;
                double cr = (c >> 16) & 255, cg = (c >> 8) & 255, cb = c & 255;
                double dr = 255 - fr, dg = 255 - fg, db = 255 - fb;
                double alpha = Math.Max(0, Math.Min(1, ((255 - cr) * dr + (255 - cg) * dg + (255 - cb) * db) / (dr * dr + dg * dg + db * db)));
                if (alpha < 0.035) continue;
                if (alpha >= 0.99) { rgba[i] = c; continue; }
                // Remove the original white contribution instead of retaining a white antialias fringe.
                int rr = Clamp((cr - (1 - alpha) * 255) / alpha), gg = Clamp((cg - (1 - alpha) * 255) / alpha), bb = Clamp((cb - (1 - alpha) * 255) / alpha);
                rgba[i] = (Clamp(alpha * 255) << 24) | (rr << 16) | (gg << 8) | bb;
            }
            // Keep the connected approved emblem; isolated matte noise is background, not part of the logo.
            bool[] visited = new bool[count]; List<int> largest = new List<int>(); Queue<int> queue = new Queue<int>();
            for (int start = 0; start < count; start++)
            {
                if (visited[start] || ((uint)rgba[start] >> 24) == 0) continue;
                List<int> component = new List<int>(); visited[start] = true; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(); component.Add(i); int x = i % w, y = i / w;
                    for (int yy = Math.Max(0, y - 1); yy <= Math.Min(h - 1, y + 1); yy++) for (int xx = Math.Max(0, x - 1); xx <= Math.Min(w - 1, x + 1); xx++)
                    { int n = yy * w + xx; if (!visited[n] && ((uint)rgba[n] >> 24) != 0) { visited[n] = true; queue.Enqueue(n); } }
                }
                if (component.Count > largest.Count) largest = component;
            }
            bool[] retained = new bool[count]; foreach (int i in largest) retained[i] = true;
            int left = w, top = h, right = -1, bottom = -1, opaque = 0, partial = 0;
            for (int i = 0; i < count; i++) if (retained[i])
            { int x = i % w, y = i / w; left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); if (((uint)rgba[i] >> 24) == 255) opaque++; else partial++; }
            if (right < left) throw new InvalidOperationException("No foreground found.");
            Rectangle bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            Rectangle padded = Rectangle.FromLTRB(Math.Max(0, left - 2), Math.Max(0, top - 2), Math.Min(w, right + 3), Math.Min(h, bottom + 3));
            using (Bitmap cropped = new Bitmap(padded.Width, padded.Height, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < padded.Height; y++) for (int x = 0; x < padded.Width; x++)
                { int i = (y + padded.Top) * w + x + padded.Left; cropped.SetPixel(x, y, retained[i] ? Color.FromArgb(rgba[i]) : Color.Transparent); }
                using (Bitmap master = new Bitmap(1024, 1024, PixelFormat.Format32bppPArgb))
                {
                    double scale = 1024.0 * 0.85 / bounds.Width;
                    int dw = (int)Math.Round(padded.Width * scale), dh = (int)Math.Round(padded.Height * scale);
                    using (Graphics g = Graphics.FromImage(master))
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceCopy; g.CompositingQuality = CompositingQuality.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; attributes.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(cropped, new Rectangle((1024 - dw) / 2, (1024 - dh) / 2, dw, dh), 0, 0, cropped.Width, cropped.Height, GraphicsUnit.Pixel, attributes);
                    }
                    master.Save(Path.Combine(assets, "eagle-eyes.png"), ImageFormat.Png);
                    int[] sizes = { 16, 20, 24, 32, 48, 64, 128, 256 }; List<byte[]> frames = new List<byte[]>();
                    foreach (int size in sizes)
                    using (Bitmap frame = Resize(master, size))
                    using (MemoryStream stream = new MemoryStream())
                    { frame.Save(stream, ImageFormat.Png); frames.Add(stream.ToArray()); frame.Save(Path.Combine(previews, "eagle-eyes-" + size + ".png"), ImageFormat.Png); }
                    // ICONDIR and eight ICONDIRENTRY records; PNG payloads are valid 32-bit ICO frames.
                    using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(assets, "eagle-eyes.ico"))))
                    {
                        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); uint offset = (uint)(6 + 16 * sizes.Length);
                        for (int i = 0; i < sizes.Length; i++)
                        { writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write((uint)frames[i].Length); writer.Write(offset); offset += (uint)frames[i].Length; }
                        foreach (byte[] frame in frames) writer.Write(frame);
                    }
                    using (Bitmap preview = new Bitmap(820, 620, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(preview))
                    using (Font font = new Font("Segoe UI", 10))
                    {
                        for (int row = 0; row < 2; row++)
                        {
                            using (SolidBrush background = new SolidBrush(row == 0 ? Color.FromArgb(246, 247, 251) : Color.FromArgb(27, 27, 43))) g.FillRectangle(background, 0, row * 310, 820, 310);
                            int x = 16;
                            foreach (int size in sizes)
                            using (Bitmap frame = Resize(master, size))
                            {
                                g.DrawImageUnscaled(frame, x, row * 310 + 34 + (256 - size) / 2);
                                using (SolidBrush ink = new SolidBrush(row == 0 ? Color.FromArgb(27, 27, 43) : Color.White)) g.DrawString(size.ToString(), font, ink, x, row * 310 + 8);
                                x += size + 20;
                            }
                        }
                        preview.Save(Path.Combine(previews, "light-dark-contact-sheet.png"), ImageFormat.Png);
                    }
                }
            }
            return new int[] { w, h, left, top, bounds.Width, bounds.Height, opaque, partial };
        }
    }
    static Bitmap Resize(Bitmap source, int size)
    {
        Bitmap output = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(output))
        { g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceCopy; g.CompositingQuality = CompositingQuality.HighQuality; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(source, new Rectangle(0, 0, size, size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel); }
        return output;
    }
}