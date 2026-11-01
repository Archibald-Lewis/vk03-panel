using VK03Panel.Core.Metrics;

namespace VK03Panel.Core.Runtime;

/// <summary>Samples on a monotonic clock, independently of animation and touch redraws.</summary>
internal sealed class MetricsTimeline(Func<MetricsSnapshot> sample, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly List<double> _cpu = new();
    private readonly List<double> _gpu = new();
    private readonly List<double> _memory = new();
    private MetricsSnapshot _snapshot = MetricsSnapshot.Empty;
    private bool _attempted;
    private long _lastAttempt;
    public long SampleCount { get; private set; }
    public IReadOnlyList<double> CpuHistory => _cpu;
    public IReadOnlyList<double> GpuHistory => _gpu;
    public IReadOnlyList<double> MemoryHistory => _memory;

    public MetricsSnapshot SampleIfDue(int refreshPerSecond)
    {
        if (TimeUntilNextSample(refreshPerSecond) > TimeSpan.Zero) return _snapshot;
        // Record attempts as well as successful samples, so a failing sensor cannot
        // turn frequent redraws into a tight retry loop.
        _lastAttempt = _clock.GetTimestamp();
        _attempted = true;
        var next = sample();
        _snapshot = next;
        SampleCount++;
        Push(_cpu, next.CpuLoad);
        Push(_gpu, next.GpuLoad);
        Push(_memory, next.MemoryTotalGb is > 0 && next.MemoryUsedGb.HasValue
            ? next.MemoryUsedGb.Value / next.MemoryTotalGb.Value : null);
        return _snapshot;
    }

    public TimeSpan TimeUntilNextSample(int refreshPerSecond)
    {
        if (!_attempted) return TimeSpan.Zero;
        var interval = TimeSpan.FromSeconds(1.0 / Math.Clamp(refreshPerSecond, 1, 2));
        var remaining = interval - _clock.GetElapsedTime(_lastAttempt, _clock.GetTimestamp());
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static void Push(List<double> history, double? value)
    {
        if (value is not { } actual || !double.IsFinite(actual)) return;
        history.Add(Math.Clamp(actual, 0, 1));
        if (history.Count > 40) history.RemoveAt(0);
    }
}
