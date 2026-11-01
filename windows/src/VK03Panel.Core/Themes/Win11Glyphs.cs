using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>
/// Fluent-style (Segoe Fluent / WinUI) glyphs for the Windows 11 theme: thin rounded
/// outlines for system icons and filled shapes for media transport.
/// </summary>
internal static class Win11Glyphs
{
    public static void Cpu(DrawingContext dc, Rect box, Brush brush)
    {
        var thickness = box.Width * 0.085;
        var pen = Paint.Stroke(brush, thickness);
        var body = new Rect(
            box.X + (box.Width * 0.18),
            box.Y + (box.Height * 0.18),
            box.Width * 0.64,
            box.Height * 0.64);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.12, box.Width * 0.12);

        var tickPen = Paint.Stroke(brush, thickness * 0.85);
        foreach (var offset in new[] { 0.32, 0.5, 0.68 })
        {
            dc.DrawLine(tickPen, At(box, offset, 0.18), At(box, offset, 0.06));
            dc.DrawLine(tickPen, At(box, offset, 0.82), At(box, offset, 0.94));
            dc.DrawLine(tickPen, At(box, 0.18, offset), At(box, 0.06, offset));
            dc.DrawLine(tickPen, At(box, 0.82, offset), At(box, 0.94, offset));
        }
    }

    public static void Memory(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.085);
        var body = new Rect(
            box.X + (box.Width * 0.06),
            box.Y + (box.Height * 0.26),
            box.Width * 0.88,
            box.Height * 0.48);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.10, box.Width * 0.10);

        var divider = Paint.Stroke(brush, box.Width * 0.075);
        dc.DrawLine(divider, At(box, 0.36, 0.32), At(box, 0.36, 0.68));
        dc.DrawLine(divider, At(box, 0.64, 0.32), At(box, 0.64, 0.68));
    }

    public static void Display(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.085);
        var body = new Rect(
            box.X + (box.Width * 0.06),
            box.Y + (box.Height * 0.12),
            box.Width * 0.88,
            box.Height * 0.52);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.11, box.Width * 0.11);
        dc.DrawLine(pen, At(box, 0.5, 0.64), At(box, 0.5, 0.80));
        dc.DrawLine(pen, At(box, 0.32, 0.84), At(box, 0.68, 0.84));
    }

    public static void MonitorMark(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.105);
        var body = new Rect(
            box.X + (box.Width * 0.06),
            box.Y + (box.Height * 0.12),
            box.Width * 0.88,
            box.Height * 0.52);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.12, box.Width * 0.12);
        dc.DrawLine(pen, At(box, 0.5, 0.64), At(box, 0.5, 0.80));
        dc.DrawLine(pen, At(box, 0.32, 0.84), At(box, 0.68, 0.84));
    }

    public static void MusicNote(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.085);
        var head = new EllipseGeometry(At(box, 0.30, 0.76), box.Width * 0.17, box.Height * 0.13);
        dc.DrawGeometry(null, pen, head);
        dc.DrawLine(pen, At(box, 0.47, 0.76), At(box, 0.47, 0.16));
        dc.DrawGeometry(null, pen, BuildPath(context =>
        {
            context.BeginFigure(At(box, 0.47, 0.16), false, false);
            context.BezierTo(At(box, 0.72, 0.22), At(box, 0.84, 0.32), At(box, 0.82, 0.50), true, false);
        }));
    }

    public static void Speaker(DrawingContext dc, Rect box, Brush brush, bool muted)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.085);
        var cone = BuildPath(context =>
        {
            context.BeginFigure(At(box, 0.08, 0.38), true, true);
            context.LineTo(At(box, 0.28, 0.38), true, false);
            context.LineTo(At(box, 0.50, 0.16), true, false);
            context.LineTo(At(box, 0.50, 0.84), true, false);
            context.LineTo(At(box, 0.28, 0.62), true, false);
            context.LineTo(At(box, 0.08, 0.62), true, false);
        });
        dc.DrawGeometry(null, pen, cone);

        if (muted)
        {
            dc.DrawLine(pen, At(box, 0.62, 0.22), At(box, 0.88, 0.78));
            return;
        }

        dc.DrawGeometry(null, pen, Arc(box, 0.60, 0.34, 0.60, 0.66, 0.16));
        dc.DrawGeometry(null, pen, Arc(box, 0.72, 0.22, 0.72, 0.78, 0.30));
    }

    public static void Minus(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.12);
        dc.DrawLine(pen, At(box, 0.18, 0.5), At(box, 0.82, 0.5));
    }

    public static void Plus(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.12);
        dc.DrawLine(pen, At(box, 0.18, 0.5), At(box, 0.82, 0.5));
        dc.DrawLine(pen, At(box, 0.5, 0.18), At(box, 0.5, 0.82));
    }

    public static void Previous(DrawingContext dc, Rect box, Brush brush)
    {
        var bar = new Rect(box.X + (box.Width * 0.18), box.Y + (box.Height * 0.16), box.Width * 0.12, box.Height * 0.68);
        dc.DrawRoundedRectangle(brush, null, bar, bar.Width / 2, bar.Width / 2);
        FillTriangle(dc, At(box, 0.84, 0.14), At(box, 0.34, 0.5), At(box, 0.84, 0.86), brush, box.Width * 0.05);
    }

    public static void Next(DrawingContext dc, Rect box, Brush brush)
    {
        var bar = new Rect(box.X + (box.Width * 0.70), box.Y + (box.Height * 0.16), box.Width * 0.12, box.Height * 0.68);
        dc.DrawRoundedRectangle(brush, null, bar, bar.Width / 2, bar.Width / 2);
        FillTriangle(dc, At(box, 0.16, 0.14), At(box, 0.66, 0.5), At(box, 0.16, 0.86), brush, box.Width * 0.05);
    }

    public static void Play(DrawingContext dc, Rect box, Brush brush)
    {
        FillTriangle(dc, At(box, 0.30, 0.14), At(box, 0.84, 0.5), At(box, 0.30, 0.86), brush, box.Width * 0.06);
    }

    public static void Pause(DrawingContext dc, Rect box, Brush brush)
    {
        var width = box.Width * 0.16;
        var left = new Rect(box.X + (box.Width * 0.28), box.Y + (box.Height * 0.14), width, box.Height * 0.72);
        var right = new Rect(box.X + (box.Width * 0.56), box.Y + (box.Height * 0.14), width, box.Height * 0.72);
        dc.DrawRoundedRectangle(brush, null, left, width / 2, width / 2);
        dc.DrawRoundedRectangle(brush, null, right, width / 2, width / 2);
    }

    public static void Lock(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.085);
        var shackle = new Rect(
            box.X + (box.Width * 0.33),
            box.Y + (box.Height * 0.16),
            box.Width * 0.34,
            box.Height * 0.36);
        dc.DrawRoundedRectangle(null, pen, shackle, shackle.Width / 2, shackle.Width / 2);

        var body = new Rect(
            box.X + (box.Width * 0.22),
            box.Y + (box.Height * 0.44),
            box.Width * 0.56,
            box.Height * 0.44);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.11, box.Width * 0.11);
        dc.DrawEllipse(brush, null, At(box, 0.5, 0.64), box.Width * 0.05, box.Width * 0.05);
    }

    private static void FillTriangle(DrawingContext dc, Point a, Point b, Point c, Brush brush, double rounding)
    {
        var geometry = BuildPath(context =>
        {
            context.BeginFigure(a, true, true);
            context.LineTo(b, true, false);
            context.LineTo(c, true, false);
        });
        var pen = new Pen(brush, rounding)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();
        dc.DrawGeometry(brush, pen, geometry);
    }

    private static PathGeometry Arc(Rect box, double x1, double y1, double x2, double y2, double radius)
    {
        var figure = new PathFigure
        {
            StartPoint = At(box, x1, y1),
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new ArcSegment(
            At(box, x2, y2),
            new Size(box.Width * radius, box.Height * radius),
            0,
            false,
            SweepDirection.Clockwise,
            true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry BuildPath(Action<StreamGeometryContext> build)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            build(context);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point At(Rect box, double x, double y) =>
        new(box.X + (box.Width * x), box.Y + (box.Height * y));
}
