using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;

namespace VK03Panel.Core.Themes;

internal enum ControlDrawingStyle { Instrument, Doodle, Manga }

/// <summary>Theme-native geometry: physical keys, pencil stickers and cut-paper player controls.</summary>
internal static class ThemeDrawing
{
    public static StreamGeometry Polygon(params Point[] points)
    {
        var shape = new StreamGeometry();
        using (var c = shape.Open())
        {
            c.BeginFigure(points[0], true, true);
            c.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        shape.Freeze(); return shape;
    }
    public static Geometry Cut(Rect r, double cut = 10) => Polygon(new Point(r.Left + cut, r.Top), new Point(r.Right, r.Top),
        new Point(r.Right, r.Bottom - cut), new Point(r.Right - cut, r.Bottom), new Point(r.Left, r.Bottom), new Point(r.Left, r.Top + cut));
    public static void Panel(DrawingContext dc, Rect r, bool dark, Brush edge)
    {
        var gradient = new LinearGradientBrush(dark ? Color.FromRgb(28, 48, 59) : Color.FromRgb(247, 252, 253),
            dark ? Color.FromRgb(10, 22, 30) : Color.FromRgb(212, 230, 234), 90);
        gradient.Freeze();
        dc.DrawGeometry(gradient, Paint.Stroke(edge, 1), Cut(r, 13));
        dc.PushOpacity(.35);
        dc.DrawLine(Paint.Stroke(edge, 1), new Point(r.Left + 14, r.Top + 3), new Point(r.Right - 3, r.Top + 3));
        dc.Pop();
        foreach (var p in new[] { new Point(r.Left + 9, r.Bottom - 9), new Point(r.Right - 9, r.Top + 9) })
        {
            dc.DrawEllipse(Paint.Brush(dark ? Color.FromRgb(8, 18, 24) : Color.FromRgb(188, 207, 211)), Paint.Stroke(edge, .5), p, 2.5, 2.5);
            dc.DrawLine(Paint.Stroke(edge, .7), new Point(p.X - 1, p.Y + 1), new Point(p.X + 1, p.Y - 1));
        }
    }
    public static void Rough(DrawingContext dc, Rect r, Brush fill, Brush ink, double stroke = 2)
    {
        if (r.Width < 24 || r.Height < 24)
        {
            dc.DrawRoundedRectangle(fill, Paint.Stroke(ink, stroke), r, 2, 2);
            return;
        }
        var shape = new StreamGeometry();
        using (var c = shape.Open())
        {
            c.BeginFigure(new Point(r.Left + 10, r.Top), true, true);
            c.BezierTo(new Point(r.Left + r.Width * .4, r.Top - 2), new Point(r.Right - 35, r.Top + 2), new Point(r.Right - 9, r.Top), true, false);
            c.QuadraticBezierTo(new Point(r.Right + 1, r.Top), new Point(r.Right, r.Top + 10), true, false);
            c.LineTo(new Point(r.Right + 1, r.Bottom - 10), true, false);
            c.QuadraticBezierTo(new Point(r.Right, r.Bottom + 1), new Point(r.Right - 11, r.Bottom), true, false);
            c.BezierTo(new Point(r.Left + r.Width * .6, r.Bottom + 2), new Point(r.Left + 35, r.Bottom - 1), new Point(r.Left + 10, r.Bottom + 1), true, false);
            c.QuadraticBezierTo(new Point(r.Left - 1, r.Bottom), new Point(r.Left, r.Bottom - 10), true, false);
            c.LineTo(new Point(r.Left - 1, r.Top + 10), true, false);
            c.QuadraticBezierTo(new Point(r.Left, r.Top), new Point(r.Left + 10, r.Top), true, false);
        }
        shape.Freeze(); dc.DrawGeometry(fill, Paint.Stroke(ink, stroke), shape);
        dc.DrawLine(Paint.Stroke(ink, .65), new Point(r.Left + 10, r.Bottom + 3), new Point(r.Left + r.Width * .48, r.Bottom + 2));
    }
    public static Point At(Point c, double radius, double angle) => new(c.X + Math.Cos(angle * Math.PI / 180) * radius, c.Y + Math.Sin(angle * Math.PI / 180) * radius);
    public static void Arc(DrawingContext dc, Point c, double radius, double from, double to, Pen pen)
    {
        if (to <= from) return;
        var f = new PathFigure { StartPoint = At(c, radius, from), IsClosed = false, IsFilled = false };
        f.Segments.Add(new ArcSegment(At(c, radius, to), new Size(radius, radius), 0, to - from > 180, SweepDirection.Clockwise, true));
        var g = new PathGeometry(new[] { f }); g.Freeze(); dc.DrawGeometry(null, pen, g);
    }
    public static void Hatch(DrawingContext dc, Rect r, Brush ink, double step = 7)
    {
        dc.PushClip(new RectangleGeometry(r, 3, 3));
        for (var x = r.Left - r.Height; x < r.Right; x += step)
            dc.DrawLine(Paint.Stroke(ink, .8), new Point(x, r.Bottom), new Point(x + r.Height, r.Top));
        dc.Pop();
    }
    private static void Key(DrawingContext dc, Rect bounds, ControlDrawingStyle style, Brush fill, Brush line, bool active, bool flower)
    {
        var r = Rect.Inflate(bounds, -5, -5);
        var shadow = Paint.Brush(Color.FromArgb(42, 0, 0, 0));
        var offset = active ? 3 : 1;
        dc.PushTransform(new TranslateTransform(0, offset));
        if (style == ControlDrawingStyle.Instrument)
        {
            dc.DrawGeometry(shadow, null, Cut(new Rect(r.X, r.Y + 2, r.Width, r.Height), 7));
            dc.DrawGeometry(fill, Paint.Stroke(line, 1), Cut(r, 7));
            dc.PushOpacity(.35);
            dc.DrawLine(Paint.Stroke(line, 1), new Point(r.Left + 8, r.Top + 3), new Point(r.Right - 3, r.Top + 3));
            dc.Pop();
        }
        else if (style == ControlDrawingStyle.Manga)
        {
            var p = Polygon(new Point(r.Left + 8, r.Top), new Point(r.Right, r.Top), new Point(r.Right - 7, r.Bottom), new Point(r.Left, r.Bottom));
            dc.PushTransform(new TranslateTransform(1, 2)); dc.DrawGeometry(shadow, null, p); dc.Pop();
            dc.DrawGeometry(fill, Paint.Stroke(line, 1.5), p);
            dc.DrawLine(Paint.Stroke(line, 2), new Point(r.Left + 2, r.Bottom - 5), new Point(r.Left + 2, r.Bottom - 12));
        }
        else if (flower)
        {
            var c = Center(r); var points = new Point[48];
            for (var i = 0; i < points.Length; i++) points[i] = At(c, r.Width / 2 + 1 + 2.5 * Math.Cos(i * Math.PI / 3), i * 360.0 / points.Length);
            dc.DrawGeometry(fill, Paint.Stroke(line, 2), Polygon(points));
        }
        else Rough(dc, r, fill, line, 1.8);
        dc.Pop();
    }
    public static void Controls(DrawingContext dc, PanelViewModel m, IPanelTheme theme, ControlDrawingStyle style,
        Brush ink, Brush accent, Brush keyFill, Brush track, Brush onAccent, bool lockLabel)
    {
        foreach (var action in new[] { PanelAction.Previous, PanelAction.Play, PanelAction.Next, PanelAction.Mute, PanelAction.Lock })
        {
            var r = theme.Bounds(action); if (r.IsEmpty) continue;
            var active = m.PressedAction == action;
            var fill = action == PanelAction.Play || active ? accent : keyFill;
            Key(dc, r, style, fill, style == ControlDrawingStyle.Doodle ? ink : track, active, action == PanelAction.Play);
            var c = Center(r); c.Y += active ? 3 : 1;
            var glyphInk = action == PanelAction.Play || active ? onAccent : ink;
            if (action == PanelAction.Play)
            {
                if (style == ControlDrawingStyle.Doodle) DoodleGlyph(dc, c, m.Media.Playing ? PanelAction.None : PanelAction.Play, glyphInk);
                else if (m.Media.Playing) MacGlyphs.Pause(dc, Box(c.X, c.Y, 23), glyphInk);
                else MacGlyphs.Play(dc, Box(c.X + 2, c.Y, 23), glyphInk);
            }
            else if (action is PanelAction.Previous or PanelAction.Next)
            {
                if (style == ControlDrawingStyle.Doodle) DoodleGlyph(dc, c, action, glyphInk);
                else if (action == PanelAction.Previous) MacGlyphs.Previous(dc, Box(c.X, c.Y, 20), glyphInk);
                else MacGlyphs.Next(dc, Box(c.X, c.Y, 20), glyphInk);
            }
            else if (action == PanelAction.Mute) Win11Glyphs.Speaker(dc, Box(c.X, c.Y, 22), glyphInk, m.Muted);
            else
            {
                Win11Glyphs.Lock(dc, Box(lockLabel ? r.Left + 20 : c.X, c.Y, 17), m.CanLock ? glyphInk : track);
                if (lockLabel) Paint.Text(dc, m.LockProgress > 0 ? "保持按住" : "按住锁屏", r.Left + 33, c.Y - 6, 10, glyphInk);
                if (m.LockProgress > 0) Paint.Bar(dc, new Rect(r.Left + 8, r.Bottom - 4, r.Width - 16, 2), m.LockProgress, track, accent);
            }
        }
        var volume = theme.Bounds(PanelAction.Volume);
        var center = Center(volume);
        var fraction = m.Muted ? 0 : Math.Clamp(m.Volume ?? 0, 0, 1);
        if (style == ControlDrawingStyle.Doodle)
        {
            var bar = new Rect(theme.SliderLeft, center.Y - 5, theme.SliderWidth, 10);
            Rough(dc, bar, keyFill, ink, 1.5);
            if (fraction > 0) Hatch(dc, new Rect(bar.Left + 2, bar.Top + 1, Math.Max(0, (bar.Width - 4) * fraction), 8), accent, 4);
            var thumb = new Point(bar.Left + bar.Width * fraction, center.Y);
            dc.DrawEllipse(accent, Paint.Stroke(ink, 1.5), thumb, 6, 9);
        }
        else
        {
            var count = style == ControlDrawingStyle.Manga ? 18 : 24;
            var step = theme.SliderWidth / count;
            for (var i = 0; i < count; i++)
            {
                var height = style == ControlDrawingStyle.Manga ? 7 + i * .35 : i % 3 == 0 ? 15 : 10;
                var r = new Rect(theme.SliderLeft + i * step, center.Y - height / 2, Math.Max(1, step - 2), height);
                dc.DrawRectangle(m.Volume.HasValue && i < fraction * count ? accent : track, null, r);
            }
            var x = theme.SliderLeft + theme.SliderWidth * fraction;
            if (m.Volume.HasValue)
                dc.DrawGeometry(accent, null, Polygon(new Point(x, center.Y - 12), new Point(x + 4, center.Y - 17), new Point(x - 4, center.Y - 17)));
        }
        if (m.PressedAction is PanelAction.Volume or PanelAction.Mute)
            Paint.Text(dc, m.Muted ? "已静音" : Percent(m.Volume), volume.Right - 54, volume.Top - 10, 10, ink);
    }
    private static void DoodleGlyph(DrawingContext dc, Point c, PanelAction a, Brush ink)
    {
        if (a == PanelAction.None)
        {
            Rough(dc, new Rect(c.X - 7, c.Y - 10, 4, 20), ink, ink, 1);
            Rough(dc, new Rect(c.X + 3, c.Y - 9, 4, 19), ink, ink, 1); return;
        }
        var next = a != PanelAction.Previous;
        var sign = next ? 1 : -1;
        dc.DrawGeometry(ink, Paint.Stroke(ink, 1.3), Polygon(new Point(c.X - 7 * sign, c.Y - 9), new Point(c.X + 8 * sign, c.Y + 1), new Point(c.X - 6 * sign, c.Y + 10)));
        if (a != PanelAction.Play) dc.DrawLine(Paint.Stroke(ink, 2.8), new Point(c.X + 10 * sign, c.Y - 9), new Point(c.X + 11 * sign, c.Y + 9));
    }
}
