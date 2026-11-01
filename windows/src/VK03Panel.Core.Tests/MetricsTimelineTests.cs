using VK03Panel.Core.Metrics;
using VK03Panel.Core.Runtime;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class MetricsTimelineTests
{
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan interval) => _timestamp += interval.Ticks;
    }
    private static MetricsSnapshot Reading() => new() { CpuLoad = .25, GpuLoad = .5, MemoryUsedGb = 8, MemoryTotalGb = 32 };

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    public void FrequentAnimationRedrawsDoNotAdvanceMetricsFaster(int rate, int expected)
    {
        var clock = new ManualClock();
        var calls = 0;
        var timeline = new MetricsTimeline(() => { calls++; return Reading(); }, clock);
        // 500 redraws during five seconds: much faster than the 80 ms lock animation.
        for (var i = 0; i < 500; i++)
        {
            timeline.SampleIfDue(rate);
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }
        Assert.Equal(expected, calls);
        Assert.Equal(expected, timeline.SampleCount);
        Assert.Equal(expected, timeline.CpuHistory.Count);
        Assert.Equal(expected, timeline.GpuHistory.Count);
        Assert.Equal(expected, timeline.MemoryHistory.Count);
    }

    [Fact]
    public void CachedSnapshotAndDeadlineSurviveExtraRedraws()
    {
        var clock = new ManualClock();
        var timeline = new MetricsTimeline(Reading, clock);
        var first = timeline.SampleIfDue(1);
        clock.Advance(TimeSpan.FromMilliseconds(80));
        Assert.Same(first, timeline.SampleIfDue(1));
        Assert.Equal(TimeSpan.FromMilliseconds(920), timeline.TimeUntilNextSample(1));
        Assert.Single(timeline.CpuHistory);
    }

    [Fact]
    public void RefreshRateChangeTakesEffectWithoutDuplicateSamples()
    {
        var clock = new ManualClock();
        var timeline = new MetricsTimeline(Reading, clock);
        timeline.SampleIfDue(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        timeline.SampleIfDue(2);
        timeline.SampleIfDue(1);
        Assert.Equal(2, timeline.SampleCount);
        clock.Advance(TimeSpan.FromSeconds(1));
        timeline.SampleIfDue(1);
        Assert.Equal(3, timeline.SampleCount);
    }

    [Fact]
    public void ResumeSamplesOnceInsteadOfCatchingUpMissedIntervals()
    {
        var clock = new ManualClock();
        var timeline = new MetricsTimeline(Reading, clock);
        timeline.SampleIfDue(1);
        clock.Advance(TimeSpan.FromMinutes(3));
        timeline.SampleIfDue(1);
        Assert.Equal(2, timeline.SampleCount);
        Assert.Equal(2, timeline.CpuHistory.Count);
    }

    [Fact]
    public void MissingValuesAreNotInsertedAndHistoryRemainsBounded()
    {
        var clock = new ManualClock();
        var timeline = new MetricsTimeline(() => new MetricsSnapshot { CpuLoad = .25, GpuLoad = double.NaN }, clock);
        for (var i = 0; i < 60; i++)
        {
            timeline.SampleIfDue(1);
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        Assert.Equal(40, timeline.CpuHistory.Count);
        Assert.Empty(timeline.GpuHistory);
        Assert.Empty(timeline.MemoryHistory);
    }

    [Fact]
    public void SensorFailureDoesNotRetryOnEveryAnimationRedraw()
    {
        var clock = new ManualClock();
        var calls = 0;
        var timeline = new MetricsTimeline(() => { calls++; throw new InvalidOperationException("sensor unavailable"); }, clock);
        Assert.Throws<InvalidOperationException>(() => timeline.SampleIfDue(1));
        for (var i = 0; i < 100; i++) timeline.SampleIfDue(1);
        Assert.Equal(1, calls);
        Assert.Equal(0, timeline.SampleCount);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Throws<InvalidOperationException>(() => timeline.SampleIfDue(1));
        Assert.Equal(2, calls);
    }
}
