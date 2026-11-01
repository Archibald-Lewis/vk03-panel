namespace VK03Panel.Core.Media;

internal static class PlaybackSeek
{
    public static bool TryGetTicks(TimeSpan start, TimeSpan end, double fraction, out long ticks)
    {
        ticks = 0;
        if (!double.IsFinite(fraction) || end <= start) return false;
        var value = (decimal)start.Ticks + ((decimal)end.Ticks - start.Ticks) * (decimal)Math.Clamp(fraction, 0, 1);
        ticks = (long)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        return true;
    }
}
