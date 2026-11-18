using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EagleEyes.App;

internal static class Program
{
    static readonly List<string> Passed = [];
    static readonly Dictionary<string,object?> Measurements = [];
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Passed.Add(message);Console.WriteLine("PASS "+message);}
    static void Reject(Action action,string message){try{action();}catch{Check(true,message);return;}throw new InvalidOperationException("Expected rejection: "+message);}
    [STAThread] static int Main(string[] args)
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var output=Path.Combine(root,".test-artifacts","branding-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        string? failure=null;
        try
        {
            LocatorTests();
            VectorTests(output);
            if(!args.Contains("--locator-only"))
            {
                var assets=Path.Combine(root,"Assets");
                var pngPath=Path.Combine(assets,"eagle-eyes.png");var icoPath=Path.Combine(assets,"eagle-eyes.ico");
                var original=Path.Combine(root,"Emblema de olhos de águia azul-ardósia.png");var canonical=Path.Combine(root,"eagle-eyes-original.png");
                Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original)).SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(canonical))),"Canonical original preserves approved source bytes");
                var bitmap=ReadBitmap(pngPath);Check(bitmap.PixelWidth==bitmap.PixelHeight,"Brand PNG is square");Check(bitmap.PixelWidth>=512,"Brand PNG is high resolution");
                var sourceMedian=MedianColor(ReadBitmap(original),true);var outputMedian=MedianColor(bitmap,false);Measurements["sourceForegroundMedianRgb"]=sourceMedian;Measurements["outputOpaqueMedianRgb"]=outputMedian;
                Check(sourceMedian.Zip(outputMedian).All(pair=>Math.Abs(pair.First-pair.Second)<=8),"Opaque logo palette remains consistent with approved source without recoloring");
                var image=Analyze(bitmap);Measurements["png"]=image;
                Check(image.Zero>0&&image.Opaque>0&&image.Semi>0,"PNG has transparent opaque and antialiased pixels");
                Check(image.WidthOccupancy>=.84&&image.WidthOccupancy<=.86,"Symbol occupies approximately85percent of square width");
                Check(Math.Abs(image.CenterX-bitmap.PixelWidth/2d)<=1.5&&Math.Abs(image.CenterY-bitmap.PixelHeight/2d)<=1.5,"Symbol boundingbox is centered");
                Check(image.InteriorHoles>=2,"Transparent interiors form at least two enclosed eye holes");
                var frames=IconTests(icoPath);Preview(bitmap,frames,Path.Combine(output,"brand-preview-light-dark.png"));
                var exeIndex=Array.IndexOf(args,"--exe");if(exeIndex>=0&&exeIndex+1<args.Length)ExeIcon(args[exeIndex+1],frames,output);
            }
        }
        catch(Exception ex){failure=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=Passed.Count,Checks=Passed,Measurements,Failure=failure},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("ARTIFACT "+output);return failure==null?0:1;
    }
    static void VectorTests(string output)
    {
        var element=(System.Windows.Controls.Viewbox)FunctionalIcons.Create(FunctionalIcon.Settings,14);
        var canvas=(System.Windows.Controls.Canvas)element.Child;var path=(System.Windows.Shapes.Path)canvas.Children[0];var geometry=path.Data;
        Check(element.Width==14&&element.Height==14&&canvas.Width==14&&canvas.Height==14,"Gear uses14DIP visual coordinate system");
        Check(path.Fill is SolidColorBrush ink&&ink.Color==Color.FromRgb(183,185,205),"Functional icons keep requested lilac gray color");
        Check(!geometry.FillContains(new Point(7,7))&&geometry.FillContains(new Point(10.3,7)),"Filled gear has a transparent center and solid rim");
        var samples=Enumerable.Range(0,720).Select(i=>geometry.FillContains(new Point(7+6.2*Math.Cos(i*Math.PI/360),7+6.2*Math.Sin(i*Math.PI/360)))).ToArray();
        var teeth=Enumerable.Range(0,720).Count(i=>samples[i]&&!samples[(i+719)%720]);Check(teeth==8,"Gear silhouette has eight distinct short teeth");
        Check(!element.IsHitTestVisible,"Vector child never intercepts parent button interaction");
        foreach(var kind in Enum.GetValues<FunctionalIcon>()){var icon=(System.Windows.Controls.Viewbox)FunctionalIcons.Create(kind,24);Check(((System.Windows.Controls.Canvas)icon.Child).Children[0] is System.Windows.Shapes.Path,"Functional icon is real vector: "+kind);}
        Reject(()=>FunctionalIcons.Create(FunctionalIcon.Settings,0),"Reject invalid vector size");
        var visual=new DrawingVisual();using(var draw=visual.RenderOpen())
        {
            draw.DrawRectangle(Brushes.White,null,new Rect(0,0,760,150));draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(27,27,43)),null,new Rect(0,150,760,150));
            foreach(var top in new[]{0d,150d}){double x=20;foreach(var kind in Enum.GetValues<FunctionalIcon>()){var icon=(System.Windows.Controls.Viewbox)FunctionalIcons.Create(kind,24);var shape=(System.Windows.Shapes.Path)((System.Windows.Controls.Canvas)icon.Child).Children[0];draw.PushTransform(new TranslateTransform(x,top+50));draw.DrawGeometry(shape.Fill,null,shape.Data);draw.Pop();x+=70;}draw.PushTransform(new TranslateTransform(620,top+15));draw.PushTransform(new ScaleTransform(8,8));draw.DrawGeometry(path.Fill,null,geometry);draw.Pop();draw.Pop();}
        }
        var bitmap=new RenderTargetBitmap(760,300,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,"functional-icons-preview.png"));encoder.Save(stream);
    }
    static void LocatorTests()
    {
        var fixture=Path.Combine(Path.GetTempPath(),"eagle-eyes-brand-qa-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);Measurements["locatorFixture"]=fixture;
        string Folder(string path){Directory.CreateDirectory(path);return path;}
        void Pair(string path){Folder(path);File.WriteAllText(Path.Combine(path,"ffmpeg.exe"),"fixture");File.WriteAllText(Path.Combine(path,"ffprobe.exe"),"fixture");}
        var app=Folder(Path.Combine(fixture,"default","app"));var binaries=Path.Combine(app,"ffmpeg");Pair(binaries);
        Check(FFmpegLocation.Resolve("ffmpeg.exe",app)==Path.Combine(binaries,"ffmpeg.exe"),"Default app ffmpeg resolves");
        Check(FFmpegLocation.Resolve("ffprobe.exe",app)==Path.Combine(binaries,"ffprobe.exe"),"Default app ffprobe resolves from same folder");
        var devRoot=Folder(Path.Combine(fixture,"dev"));var nested=Folder(Path.Combine(devRoot,"src","app","bin","Debug"));var devBinaries=Path.Combine(devRoot,"components","ffmpeg-master-latest-win64-gpl","bin");Pair(devBinaries);
        Check(FFmpegLocation.Resolve("ffmpeg.exe",nested)==Path.Combine(devBinaries,"ffmpeg.exe"),"Development ancestor pair resolves");
        Check(FFmpegLocation.Resolve("ffprobe.exe",nested)==Path.Combine(devBinaries,"ffprobe.exe"),"Development probe stays beside encoder");
        var local=Path.Combine(nested,"ffmpeg");Pair(local);Check(FFmpegLocation.Resolve("ffmpeg.exe",nested)==Path.Combine(local,"ffmpeg.exe"),"Complete application pair takes priority over ancestor");
        File.Delete(Path.Combine(local,"ffprobe.exe"));Check(FFmpegLocation.Resolve("ffmpeg.exe",nested)==Path.Combine(devBinaries,"ffmpeg.exe"),"Incomplete application pair is never mixed with ancestor probe");
        var mixedRoot=Folder(Path.Combine(fixture,"mixed"));var mixedApp=Folder(Path.Combine(mixedRoot,"app"));Folder(Path.Combine(mixedApp,"ffmpeg"));File.WriteAllText(Path.Combine(mixedApp,"ffmpeg","ffmpeg.exe"),"fixture");var mixedDev=Folder(Path.Combine(mixedRoot,"components","ffmpeg-master-latest-win64-gpl","bin"));File.WriteAllText(Path.Combine(mixedDev,"ffprobe.exe"),"fixture");
        Reject(()=>FFmpegLocation.Resolve("ffmpeg.exe",mixedApp),"Reject pair split across two folders");
        var emptyApp=Folder(Path.Combine(fixture,"empty-app"));var unrelated=Folder(Path.Combine(fixture,"unrelated"));Pair(Path.Combine(unrelated,"ffmpeg"));var pathPair=Folder(Path.Combine(fixture,"path-only"));Pair(pathPair);
        var oldDirectory=Environment.CurrentDirectory;var oldPath=Environment.GetEnvironmentVariable("PATH");
        try{Environment.CurrentDirectory=unrelated;Environment.SetEnvironmentVariable("PATH",pathPair);Reject(()=>FFmpegLocation.Resolve("ffmpeg.exe",emptyApp),"Ignore unrelated CWD and PATH candidates");}
        finally{Environment.CurrentDirectory=oldDirectory;Environment.SetEnvironmentVariable("PATH",oldPath);}
        Reject(()=>FFmpegLocation.Resolve("ffplay.exe",app),"Reject unsupported executable name");
        Reject(()=>FFmpegLocation.Resolve("../ffmpeg.exe",app),"Reject executable path traversal");
    }
    static BitmapSource ReadBitmap(string path){using var stream=File.OpenRead(path);var source=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];source.Freeze();return source;}
    static int[] MedianColor(BitmapSource source,bool sourceOnWhite)
    {
        var image=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);var colors=new[]{new int[256],new int[256],new int[256]};int count=0;
        for(int i=0;i<pixels.Length;i+=4){if(pixels[i+3]<250||(sourceOnWhite&&Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]))>210))continue;colors[0][pixels[i+2]]++;colors[1][pixels[i+1]]++;colors[2][pixels[i]]++;count++;}
        return colors.Select(histogram=>{int sum=0;for(int value=0;value<256;value++){sum+=histogram[value];if(sum>=count/2)return value;}return 255;}).ToArray();
    }
    sealed record AlphaAnalysis(int Width,int Height,int Zero,int Opaque,int Semi,int InteriorHoles,double WidthOccupancy,double CenterX,double CenterY,int MinX,int MinY,int MaxX,int MaxY);
    static AlphaAnalysis Analyze(BitmapSource source)
    {
        var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var w=bitmap.PixelWidth;var h=bitmap.PixelHeight;var bytes=new byte[w*h*4];bitmap.CopyPixels(bytes,w*4,0);
        int zero=0,opaque=0,semi=0,minX=w,minY=h,maxX=-1,maxY=-1;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){var a=bytes[(y*w+x)*4+3];if(a==0)zero++;else if(a==255)opaque++;else semi++;if(a>4){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}}
        var visited=new bool[w*h];var queue=new Queue<int>();
        bool Transparent(int i)=>bytes[i*4+3]<16;
        void Visit(int i){if(i>=0&&i<w*h&&!visited[i]&&Transparent(i)){visited[i]=true;queue.Enqueue(i);}}
        int Flood(int first){Visit(first);int count=0;while(queue.Count>0){var i=queue.Dequeue();count++;var x=i%w;var y=i/w;if(x>0)Visit(i-1);if(x+1<w)Visit(i+1);if(y>0)Visit(i-w);if(y+1<h)Visit(i+w);}return count;}
        for(int x=0;x<w;x++){if(!visited[x])Flood(x);var i=(h-1)*w+x;if(!visited[i])Flood(i);}for(int y=0;y<h;y++){var i=y*w;if(!visited[i])Flood(i);i+=w-1;if(!visited[i])Flood(i);}
        int holes=0;for(int i=0;i<w*h;i++)if(!visited[i]&&Transparent(i)&&Flood(i)>Math.Max(4,w*h/100000))holes++;
        return new(w,h,zero,opaque,semi,holes,(maxX-minX+1)/(double)w,(minX+maxX+1)/2d,(minY+maxY+1)/2d,minX,minY,maxX,maxY);
    }
    static BitmapSource[] IconTests(string path)
    {
        var bytes=File.ReadAllBytes(path);using var stream=new MemoryStream(bytes);using var reader=new BinaryReader(stream);
        Check(reader.ReadUInt16()==0&&reader.ReadUInt16()==1,"ICO has real icon container header");var count=reader.ReadUInt16();Check(count==8,"ICO has eight directory entries");
        var expected=new[]{16,20,24,32,48,64,128,256};var ranges=new List<(uint Start,uint End)>();
        for(int i=0;i<count;i++){int width=reader.ReadByte();int height=reader.ReadByte();width=width==0?256:width;height=height==0?256:height;reader.ReadByte();Check(reader.ReadByte()==0,"ICO reserved entry byte is zero for"+expected[i]);var planes=reader.ReadUInt16();var bits=reader.ReadUInt16();var length=reader.ReadUInt32();var offset=reader.ReadUInt32();Check(width==expected[i]&&height==expected[i],"ICO directory dimensions"+expected[i]);Check(planes==1&&bits==32,"ICO entry is32bit"+expected[i]);Check(length>0&&offset>=6+16*count&&(ulong)offset+length<=(ulong)bytes.Length,"ICO payload range valid"+expected[i]);Check(ranges.All(r=>offset>=r.End||offset+length<=r.Start),"ICO payloads do not overlap"+expected[i]);ranges.Add((offset,offset+length));}
        using var input=File.OpenRead(path);var decoder=new IconBitmapDecoder(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);Check(decoder.Frames.Count==8,"Windows decoder reads eight real ICO frames");var frames=decoder.Frames.Cast<BitmapSource>().ToArray();foreach(var size in expected){var frame=frames.Single(f=>f.PixelWidth==size&&f.PixelHeight==size);var alpha=Analyze(frame);Check(alpha.Zero>0&&alpha.Opaque>0&&alpha.Semi>0,"ICO alpha and antialias retained at"+size);frame.Freeze();}return frames;
    }
    static void Preview(BitmapSource high,BitmapSource[] icons,string path)
    {
        var visual=new DrawingVisual();using(var draw=visual.RenderOpen())
        {
            draw.DrawRectangle(Brushes.White,null,new Rect(0,0,1100,300));draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(27,27,43)),null,new Rect(0,300,1100,300));
            foreach(var top in new[]{0d,300d}){double x=18;foreach(var icon in icons){draw.DrawImage(icon,new Rect(x,top+(300-icon.PixelHeight)/2,icon.PixelWidth,icon.PixelHeight));x+=icon.PixelWidth+24;}draw.DrawImage(high,new Rect(850,top+30,240,240));}
        }
        var target=new RenderTargetBitmap(1100,600,96,96,PixelFormats.Pbgra32);target.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using var output=File.Create(path);encoder.Save(output);
    }
    static void ExeIcon(string path,BitmapSource[] frames,string output)
    {
        var count=ExtractIconEx(Path.GetFullPath(path),0,out var large,out var small,1);
        try
        {
            Check(count>0&&large!=IntPtr.Zero&&small!=IntPtr.Zero,"Shell extracts native large and small EXE HICON resources");
            var bitmap=Imaging.CreateBitmapSourceFromHIcon(small,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());var image=Analyze(bitmap);Measurements["exeIcon"]=image;
            Check(image.Zero>0&&image.Opaque>0,"Native EXE icon preserves transparency");
            var expected=frames.OrderBy(frame=>Math.Abs(frame.PixelWidth-bitmap.PixelWidth)).First();
            Check(expected.PixelWidth==bitmap.PixelWidth&&expected.PixelHeight==bitmap.PixelHeight,"Shell HICON has an official matching resolution");
            var nativePixels=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var expectedPixels=new FormatConvertedBitmap(expected,PixelFormats.Bgra32,null,0);var actualBytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];var expectedBytes=new byte[actualBytes.Length];nativePixels.CopyPixels(actualBytes,bitmap.PixelWidth*4,0);expectedPixels.CopyPixels(expectedBytes,bitmap.PixelWidth*4,0);
            int different=0,union=0;for(int i=3;i<actualBytes.Length;i+=4){bool actual=actualBytes[i]>127,reference=expectedBytes[i]>127;if(actual||reference)union++;if(actual!=reference)different++;}
            double maskError=different/(double)Math.Max(1,union);Measurements["nativeIconMaskError"]=maskError;Check(maskError<.03,"Native EXE icon silhouette matches actual official ICO frame");
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var imageFile=File.Create(Path.Combine(output,"native-exe-icon.png")))encoder.Save(imageFile);
            var expectedAlpha=Analyze(expected);Check(Math.Abs(image.WidthOccupancy-expectedAlpha.WidthOccupancy)<.12,"Native EXE icon silhouette matches official ICO occupancy");
        }
        finally{if(large!=IntPtr.Zero)DestroyIcon(large);if(small!=IntPtr.Zero)DestroyIcon(small);}
    }
}





