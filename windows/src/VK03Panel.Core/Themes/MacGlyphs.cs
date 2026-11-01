using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>
/// Vector glyphs for the macOS theme, approximating the SF Symbols used by the reference
/// renderer. Everything is drawn from geometry so the panel never depends on font icon coverage.
/// </summary>
internal static class MacGlyphs
{
    public static void Monitor(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.11);
        var body = new Rect(
            box.X + (box.Width * 0.07),
            box.Y + (box.Height * 0.08),
            box.Width * 0.86,
            box.Height * 0.54);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.12, box.Width * 0.12);
        dc.DrawLine(pen, PointAt(box, 0.5, 0.62), PointAt(box, 0.5, 0.76));
        dc.DrawLine(pen, PointAt(box, 0.30, 0.84), PointAt(box, 0.70, 0.84));
    }

    public static void Cpu(DrawingContext dc, Rect box, Brush brush)
    {
        var outer = new Rect(
            box.X + (box.Width * 0.09),
            box.Y + (box.Height * 0.09),
            box.Width * 0.82,
            box.Height * 0.82);
        dc.DrawRoundedRectangle(null, Paint.Stroke(brush, box.Width * 0.12), outer, box.Width * 0.14, box.Width * 0.14);
        var inner = new Rect(
            box.X + (box.Width * 0.31),
            box.Y + (box.Height * 0.31),
            box.Width * 0.38,
            box.Height * 0.38);
        dc.DrawRoundedRectangle(null, Paint.Stroke(brush, box.Width * 0.085), inner, box.Width * 0.07, box.Width * 0.07);
    }

    public static void Memory(DrawingContext dc, Rect box, Brush brush)
    {
        var body = new Rect(
            box.X + (box.Width * 0.02),
            box.Y + (box.Height * 0.27),
            box.Width * 0.96,
            box.Height * 0.46);
        dc.DrawRoundedRectangle(null, Paint.Stroke(brush, box.Width * 0.11), body, box.Width * 0.12, box.Width * 0.12);
    }

    public static void Display(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.11);
        var body = new Rect(
            box.X + (box.Width * 0.05),
            box.Y + (box.Height * 0.08),
            box.Width * 0.90,
            box.Height * 0.56);
        dc.DrawRoundedRectangle(null, pen, body, box.Width * 0.13, box.Width * 0.13);
        dc.DrawLine(pen, PointAt(box, 0.5, 0.64), PointAt(box, 0.5, 0.78));
        dc.DrawLine(pen, PointAt(box, 0.32, 0.85), PointAt(box, 0.68, 0.85));
    }

    public static void MusicNote(DrawingContext dc, Rect box, Brush brush)
    {
        var head = new EllipseGeometry(PointAt(box, 0.29, 0.80), box.Width * 0.25, box.Height * 0.18);
        dc.DrawGeometry(brush, null, head);

        var stem = new Rect(
            box.X + (box.Width * 0.43),
            box.Y + (box.Height * 0.13),
            box.Width * 0.115,
            box.Height * 0.69);
        dc.DrawRoundedRectangle(brush, null, stem, stem.Width / 2, stem.Width / 2);

        var flag = BuildPath(context =>
        {
            context.BeginFigure(PointAt(box, 0.545, 0.13), true, true);
            context.BezierTo(PointAt(box, 0.80, 0.18), PointAt(box, 0.92, 0.32), PointAt(box, 0.90, 0.54), true, false);
            context.BezierTo(PointAt(box, 0.76, 0.35), PointAt(box, 0.64, 0.33), PointAt(box, 0.545, 0.42), true, false);
        });
        dc.DrawGeometry(brush, null, flag);
    }

    public static void Speaker(DrawingContext dc, Rect box, Brush brush, bool muted)
    {
        var cone = BuildPath(context =>
        {
            context.BeginFigure(PointAt(box, 0.04, 0.30), true, true);
            context.LineTo(PointAt(box, 0.28, 0.30), true, false);
            context.LineTo(PointAt(box, 0.54, 0.10), true, false);
            context.LineTo(PointAt(box, 0.54, 0.90), true, false);
            context.LineTo(PointAt(box, 0.28, 0.70), true, false);
            context.LineTo(PointAt(box, 0.04, 0.70), true, false);
        });
        dc.DrawGeometry(brush, null, cone);

        if (muted)
        {
            var pen = Paint.Stroke(brush, box.Width * 0.11);
            dc.DrawLine(pen, PointAt(box, 0.63, 0.20), PointAt(box, 0.92, 0.80));
            return;
        }

        var wavePen = Paint.Stroke(brush, box.Width * 0.09);
        dc.DrawGeometry(null, wavePen, Arc(box, 0.64, 0.30, 0.64, 0.70, 0.20));
        dc.DrawGeometry(null, wavePen, Arc(box, 0.76, 0.18, 0.76, 0.82, 0.32));
    }

    public static void Minus(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.14);
        dc.DrawLine(pen, PointAt(box, 0.13, 0.5), PointAt(box, 0.87, 0.5));
    }

    public static void Plus(DrawingContext dc, Rect box, Brush brush)
    {
        var pen = Paint.Stroke(brush, box.Width * 0.14);
        dc.DrawLine(pen, PointAt(box, 0.13, 0.5), PointAt(box, 0.87, 0.5));
        dc.DrawLine(pen, PointAt(box, 0.5, 0.13), PointAt(box, 0.5, 0.87));
    }

    public static void Previous(DrawingContext dc, Rect box, Brush brush)
    {
        var bar = new Rect(box.X + (box.Width * 0.15), box.Y + (box.Height * 0.11), box.Width * 0.14, box.Height * 0.78);
        dc.DrawRoundedRectangle(brush, null, bar, bar.Width / 2, bar.Width / 2);
        FillTriangle(dc, PointAt(box, 0.88, 0.10), PointAt(box, 0.32, 0.5), PointAt(box, 0.88, 0.90), brush, box.Width * 0.075);
    }

    public static void Next(DrawingContext dc, Rect box, Brush brush)
    {
        var bar = new Rect(box.X + (box.Width * 0.71), box.Y + (box.Height * 0.11), box.Width * 0.14, box.Height * 0.78);
        dc.DrawRoundedRectangle(brush, null, bar, bar.Width / 2, bar.Width / 2);
        FillTriangle(dc, PointAt(box, 0.12, 0.10), PointAt(box, 0.68, 0.5), PointAt(box, 0.12, 0.90), brush, box.Width * 0.075);
    }

    public static void Play(DrawingContext dc, Rect box, Brush brush)
    {
        FillTriangle(dc, PointAt(box, 0.22, 0.08), PointAt(box, 0.92, 0.5), PointAt(box, 0.22, 0.92), brush, box.Width * 0.085);
    }

    public static void Pause(DrawingContext dc, Rect box, Brush brush)
    {
        var width = box.Width * 0.18;
        var left = new Rect(box.X + (box.Width * 0.25), box.Y + (box.Height * 0.12), width, box.Height * 0.76);
        var right = new Rect(box.X + (box.Width * 0.57), box.Y + (box.Height * 0.12), width, box.Height * 0.76);
        dc.DrawRoundedRectangle(brush, null, left, width / 2, width / 2);
        dc.DrawRoundedRectangle(brush, null, right, width / 2, width / 2);
    }

    public static void Lock(DrawingContext dc, Rect box, Brush brush, Brush cutout)
    {
        var shackle = new Rect(
            box.X + (box.Width * 0.32),
            box.Y + (box.Height * 0.14),
            box.Width * 0.36,
            box.Height * 0.40);
        dc.DrawRoundedRectangle(null, Paint.Stroke(brush, box.Width * 0.11), shackle, shackle.Width / 2, shackle.Width / 2);

        var body = new Rect(
            box.X + (box.Width * 0.17),
            box.Y + (box.Height * 0.42),
            box.Width * 0.66,
            box.Height * 0.48);
        dc.DrawRoundedRectangle(brush, null, body, box.Width * 0.12, box.Width * 0.12);

        var hole = new EllipseGeometry(PointAt(box, 0.5, 0.62), box.Width * 0.04, box.Width * 0.04);
        dc.DrawGeometry(cutout, null, hole);
        var stem = new Rect(
            box.X + (box.Width * 0.475),
            box.Y + (box.Height * 0.62),
            box.Width * 0.05,
            box.Height * 0.14);
        dc.DrawRoundedRectangle(cutout, null, stem, stem.Width / 2, stem.Width / 2);
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
            StartPoint = PointAt(box, x1, y1),
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new ArcSegment(
            PointAt(box, x2, y2),
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

    internal static Point PointAt(Rect box, double x, double y) =>
        new(box.X + (box.Width * x), box.Y + (box.Height * y));
}
