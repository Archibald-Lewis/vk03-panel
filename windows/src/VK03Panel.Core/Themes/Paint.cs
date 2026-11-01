using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

public enum TextWeight
{
    Light,
    Regular,
    Medium,
    SemiBold,
    Bold,
}

/// <summary>
/// Shared drawing primitives for the panel themes. Everything is drawn at 1:1 pixel
/// density into a 960×360 (or 360×960) surface, so sizes are physical pixels.
/// </summary>
public static class Paint
{
    /// <summary>Ratio of the reference (SF Pro) ascender to font size; keeps text vertically aligned with the macOS layout.</summary>
    public const double AscenderRatio = 0.95;

    public static readonly FontFamily DisplayFamily = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily TextFamily = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily SmallFamily = new("Segoe UI Variable Small, Segoe UI");

    private static readonly Dictionary<Color, SolidColorBrush> BrushCache = new();
    private static readonly object BrushSync = new();

    public static SolidColorBrush Brush(Color color)
    {
        lock (BrushSync)
        {
            if (BrushCache.TryGetValue(color, out var cached))
            {
                return cached;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            BrushCache[color] = brush;
            return brush;
        }
    }

    public static SolidColorBrush Brush(byte r, byte g, byte b, byte a = 255) => Brush(Color.FromArgb(a, r, g, b));

    /// <summary>Creates a colour from 0..1 components, the same way the macOS renderer does.</summary>
    public static Color ColorRgb(double r, double g, double b, double a = 1)
    {
        return Color.FromArgb(
            (byte)Math.Round(Math.Clamp(a, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(r, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(g, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(b, 0, 1) * 255));
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    public static SolidColorBrush BrushFrom(double r, double g, double b, double a = 1) => Brush(ColorRgb(r, g, b, a));

    public static Pen Stroke(Brush brush, double width)
    {
        var pen = new Pen(brush, width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    public static void RoundedRect(DrawingContext dc, Rect rect, double radius, Brush? fill)
    {
        dc.DrawRoundedRectangle(fill, null, rect, radius, radius);
    }

    public static void RoundedRect(DrawingContext dc, Rect rect, double radius, Brush? fill, Pen? pen)
    {
        dc.DrawRoundedRectangle(fill, pen, rect, radius, radius);
    }

    /// <summary>Rounded progress bar, identical in behaviour to the macOS helper of the same name.</summary>
    public static void Bar(DrawingContext dc, Rect rect, double fraction, Brush track, Brush fill)
    {
        RoundedRect(dc, rect, rect.Height / 2, track);
        var clamped = Math.Clamp(fraction, 0, 1);
        if (clamped <= 0)
        {
            return;
        }

        var filled = new Rect(rect.X, rect.Y, rect.Width * clamped, rect.Height);
        RoundedRect(dc, filled, rect.Height / 2, fill);
    }

    public static FontFamily FontFor(double size) =>
        size >= 24 ? DisplayFamily : size >= 12 ? TextFamily : SmallFamily;

    public static FontWeight WeightFor(TextWeight weight) => weight switch
    {
        TextWeight.Light => FontWeights.Light,
        TextWeight.Medium => FontWeights.Medium,
        TextWeight.SemiBold => FontWeights.SemiBold,
        TextWeight.Bold => FontWeights.Bold,
        _ => FontWeights.Normal,
    };

    private static FormattedText Build(
        string text,
        double size,
        Brush brush,
        TextWeight weight,
        FontFamily? family,
        double? maxWidth)
    {
        var typeface = new Typeface(family ?? FontFor(size), FontStyles.Normal, WeightFor(weight), FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            1.0)
        {
            MaxLineCount = 1,
            MaxTextWidth = maxWidth ?? 10000,
        };

        if (maxWidth.HasValue)
        {
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    /// <summary>
    /// Draws a single line of text. <paramref name="top"/> is the top of the reference line box
    /// (baseline at top + 0.95em), matching the macOS renderer's coordinates.
    /// </summary>
    public static void Text(
        DrawingContext dc,
        string text,
        double left,
        double top,
        double size,
        Brush brush,
        TextWeight weight = TextWeight.Regular,
        bool tabular = false,
        double? maxWidth = null,
        FontFamily? family = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (tabular && NeedsTabularAlignment(text))
        {
            DrawTabular(dc, text, left, top, size, brush, weight, family);
            return;
        }

        var formatted = Build(text, size, brush, weight, family, maxWidth);
        var originY = top + (size * AscenderRatio) - formatted.Baseline;
        dc.DrawText(formatted, new Point(left, originY));
    }

    private static bool NeedsTabularAlignment(string text)
    {
        foreach (var character in text)
        {
            if (character is >= '0' and <= '9')
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawTabular(
        DrawingContext dc,
        string text,
        double left,
        double top,
        double size,
        Brush brush,
        TextWeight weight,
        FontFamily? family)
    {
        var reference = Build("0", size, brush, weight, family, null);
        var advance = reference.WidthIncludingTrailingWhitespace;
        var originY = top + (size * AscenderRatio) - reference.Baseline;
        var x = left;

        foreach (var character in text)
        {
            if (character is >= '0' and <= '9')
            {
                var digit = Build(character.ToString(), size, brush, weight, family, null);
                var offset = (advance - digit.WidthIncludingTrailingWhitespace) / 2;
                dc.DrawText(digit, new Point(x + offset, originY));
                x += advance;
                continue;
            }

            if (character == ' ')
            {
                x += advance / 2;
                continue;
            }

            // Punctuation keeps its natural width so clock separators stay tight.
            var formatted = Build(character.ToString(), size, brush, weight, family, null);
            dc.DrawText(formatted, new Point(x, originY));
            x += formatted.WidthIncludingTrailingWhitespace;
        }
    }

    /// <summary>Draws a line of text horizontally centred on <paramref name="centerX"/>.</summary>
    public static void CenteredText(
        DrawingContext dc,
        string text,
        double centerX,
        double top,
        double size,
        Brush brush,
        TextWeight weight = TextWeight.Regular,
        bool tabular = false,
        FontFamily? family = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var width = MeasureText(text, size, weight, tabular, family);
        Text(dc, text, centerX - (width / 2), top, size, brush, weight, tabular, null, family);
    }

    public static double MeasureText(
        string text,
        double size,
        TextWeight weight = TextWeight.Regular,
        bool tabular = false,
        FontFamily? family = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        if (tabular)
        {
            var reference = Build("0", size, Brushes.White, weight, family, null);
            var advance = reference.WidthIncludingTrailingWhitespace;
            var total = 0.0;
            foreach (var character in text)
            {
                if (character is >= '0' and <= '9')
                {
                    total += advance;
                }
                else if (character != ' ')
                {
                    total += Build(character.ToString(), size, Brushes.White, weight, family, null).WidthIncludingTrailingWhitespace;
                }
                else
                {
                    total += advance / 2;
                }
            }

            return total;
        }

        var formatted = Build(text, size, Brushes.White, weight, family, null);
        return formatted.WidthIncludingTrailingWhitespace;
    }

    public static void LinearGradient(DrawingContext dc, Rect rect, Point start, Point end, params (double Offset, Color Color)[] stops)
    {
        var gradient = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var stop in stops)
        {
            gradient.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        }

        gradient.Freeze();
        dc.DrawRectangle(gradient, null, rect);
    }

    /// <summary>Radial accent pool used by both themes to give the backdrop depth.</summary>
    public static void RadialGlow(DrawingContext dc, Point center, double radius, Color color, double alpha)
    {
        var brush = new RadialGradientBrush(WithAlpha(color, alpha), WithAlpha(color, 0));
        brush.Freeze();
        dc.DrawRectangle(brush, null, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
    }

    public static void ClipRounded(DrawingContext dc, Rect rect, double radius, Action draw)
    {
        dc.PushClip(new RectangleGeometry(rect, radius, radius));
        try
        {
            draw();
        }
        finally
        {
            dc.Pop();
        }
    }

    /// <summary>Filled sparkline with a soft gradient area and a crisp top line.</summary>
    public static void Sparkline(DrawingContext dc, Rect rect, IReadOnlyList<double>? values, Color color)
    {
        if (values is null || values.Count < 2 || rect.Width <= 2 || rect.Height <= 2)
        {
            return;
        }

        var count = values.Count;
        var step = rect.Width / (count - 1);

        double Value(int index)
        {
            var raw = values[index];
            return double.IsFinite(raw) ? Math.Clamp(raw, 0, 1) : 0;
        }

        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(rect.X, rect.Bottom), true, true);
            for (var index = 0; index < count; index++)
            {
                context.LineTo(new Point(rect.X + (index * step), rect.Bottom - (rect.Height * Value(index))), true, false);
            }

            context.LineTo(new Point(rect.Right, rect.Bottom), true, false);
        }

        area.Freeze();
        var fill = new LinearGradientBrush(WithAlpha(color, 0.28), WithAlpha(color, 0.01), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(new Point(rect.X, rect.Bottom - (rect.Height * Value(0))), false, false);
            for (var index = 1; index < count; index++)
            {
                context.LineTo(new Point(rect.X + (index * step), rect.Bottom - (rect.Height * Value(index))), true, false);
            }
        }

        line.Freeze();
        var pen = new Pen(Brush(WithAlpha(color, 0.9)), 1.6)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);

        dc.DrawLine(
            Stroke(Brush(WithAlpha(color, 0.12)), 1),
            new Point(rect.X, rect.Bottom + 0.5),
            new Point(rect.Right, rect.Bottom + 0.5));
    }
}
