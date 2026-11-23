using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
namespace EagleEyes.App;

internal enum FunctionalIcon { Settings, Close, Video, Pause, Play, Stop, ChevronRight, Folder, Camera, Pencil }
internal static class FunctionalIcons
{
    static readonly Brush Ink = FrozenBrush();
    static Brush FrozenBrush() { var brush = new SolidColorBrush(Color.FromRgb(183, 185, 205)); brush.Freeze(); return brush; }

    internal static FrameworkElement Create(FunctionalIcon kind, double size)
    {
        if (!double.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Geometry geometry = kind switch
        {
            FunctionalIcon.Settings => Gear(),
            FunctionalIcon.Close => Close(),
            FunctionalIcon.Pause => new GeometryGroup { Children = { new RectangleGeometry(new Rect(5, 3, 4, 18), 1.6, 1.6), new RectangleGeometry(new Rect(15, 3, 4, 18), 1.6, 1.6) } },
            FunctionalIcon.Play => RoundedTriangle(),
            FunctionalIcon.Stop => new RectangleGeometry(new Rect(4, 4, 16, 16), 2, 2),
            FunctionalIcon.Camera => Stroke(Geometry.Parse("M2,7 L7,7 L9,4 L15,4 L17,7 L22,7 L22,20 L2,20 Z M16,13 A4,4 0 1 1 8,13 A4,4 0 1 1 16,13"), 1.8),
            FunctionalIcon.Pencil => Stroke(Geometry.Parse("M3,21 L5,15 L17,3 L21,7 L9,19 Z M15,5 L19,9"), 1.8),
            FunctionalIcon.Video => Video(),
            FunctionalIcon.ChevronRight => Stroke(Geometry.Parse("M8,4 L16,12 L8,20"), 2.3),
            FunctionalIcon.Folder => Stroke(Geometry.Parse("M3,7 L3,5 Q3,3 5,3 L10,3 L13,6 L20,6 Q22,6 22,8 L22,19 Q22,21 20,21 L4,21 Q2,21 2,19 L2,9 Q2,7 4,7 L22,7"), 1.7),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        geometry.Freeze();
        // A fixed coordinate system keeps optical sizes independent of geometry bounds.
        double extent = kind is FunctionalIcon.Settings or FunctionalIcon.Close ? 14 : 24;
        var canvas = new System.Windows.Controls.Canvas { Width = extent, Height = extent };
        canvas.Children.Add(new Path { Data = geometry, Fill = Ink });
        return new System.Windows.Controls.Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = canvas, IsHitTestVisible = false };
    }
    static Geometry Stroke(Geometry geometry, double thickness)
    {
        return geometry.GetWidenedPathGeometry(new Pen(Ink, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round });
    }
    static Geometry Close()
    {
        var first = new RectangleGeometry(new Rect(5.9, -1, 2.2, 16), 1.1, 1.1) { Transform = new RotateTransform(45, 7, 7) };
        var second = new RectangleGeometry(new Rect(5.9, -1, 2.2, 16), 1.1, 1.1) { Transform = new RotateTransform(-45, 7, 7) };
        return Geometry.Combine(first, second, GeometryCombineMode.Union, null);
    }
    static Geometry Gear()
    {
        var vertices = new List<Point>();
        for (int tooth = 0; tooth < 8; tooth++)
        {
            foreach (var (offset, radius) in new[] { (-22.5, 5.15), (-11d, 6.8), (11d, 6.8), (20d, 5.15) })
            {
                double angle = (tooth * 45 + offset) * Math.PI / 180;
                vertices.Add(new Point(7 + Math.Cos(angle) * radius, 7 + Math.Sin(angle) * radius));
            }
        }
        Point Toward(Point current, Point target)
        {
            var vector = target - current; double distance = Math.Min(.36, vector.Length * .35);
            vector.Normalize(); return current + vector * distance;
        }
        var outline = new StreamGeometry();
        using (var context = outline.Open())
        {
            context.BeginFigure(Toward(vertices[0], vertices[^1]), true, true);
            for (int index = 0; index < vertices.Count; index++)
            {
                var current = vertices[index]; var previous = vertices[(index + vertices.Count - 1) % vertices.Count]; var next = vertices[(index + 1) % vertices.Count];
                context.LineTo(Toward(current, previous), true, false);
                context.QuadraticBezierTo(current, Toward(current, next), true, true);
            }
        }
        return Geometry.Combine(outline, new EllipseGeometry(new Point(7, 7), 2.35, 2.35), GeometryCombineMode.Exclude, null);
    }
    static Geometry RoundedTriangle()
    {
        return Geometry.Parse("M6,3.8 Q6,2.2 7.5,3 L21,10.8 Q22.5,12 21,13.2 L7.5,21 Q6,21.8 6,20.2 Z");
    }
    static Geometry Video()
    {
        var outline = new GeometryGroup();
        outline.Children.Add(new RectangleGeometry(new Rect(2, 5, 13, 14), 2.2, 2.2));
        outline.Children.Add(Geometry.Parse("M15,9 L22,5.5 L22,18.5 L15,15"));
        return Stroke(outline, 1.8);
    }
}


