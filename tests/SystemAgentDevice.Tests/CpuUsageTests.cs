using SystemAgentExample.Communication.Collectors;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// CPU usage must be the share of non-idle time over the window between two
/// samples of the aggregate <c>cpu</c> line of /proc/stat — not the average since
/// boot, which a single read of the cumulative counters yields.
/// </summary>
public class CpuUsageTests
{
    private const string ProcStat =
        "cpu  10132153 290696 3084719 46828483 16683 0 25195 0 175628 0\n" +
        "cpu0 1393280 32966 572056 13343292 6130 0 17875 0 23933 0\n" +
        "cpu1 1335400 30000 500000 11000000 5000 0 3000 0 20000 0\n" +
        "intr 1462898 0 0 0\n" +
        "ctxt 2440328\n" +
        "btime 1761100000\n";

    private static CpuTimes Times(long user = 0, long nice = 0, long system = 0, long idle = 0,
        long iowait = 0, long irq = 0, long softirq = 0, long steal = 0)
        => new(user, nice, system, idle, iowait, irq, softirq, steal);

    // --- Parsing ---

    [Fact]
    public void ParseAggregate_ReadsTheCpuLine_IgnoringPerCoreLinesAndGuestFields()
    {
        var times = CpuTimes.ParseAggregate(ProcStat);

        Assert.Equal(Times(10132153, 290696, 3084719, 46828483, 16683, 0, 25195, 0), times);
        // guest / guest_nice (175628, 0) are already part of user / nice
        Assert.Equal(10132153L + 290696 + 3084719 + 46828483 + 16683 + 25195, times!.Value.Total);
    }

    [Theory]
    [InlineData("intr 1462898 0 0 0\nctxt 2440328\n")]
    [InlineData("cpu  1 2 3\n")]
    [InlineData("cpu  a b c d e f g h\n")]
    public void ParseAggregate_MissingOrMalformedCpuLine_ReturnsNull(string content)
    {
        Assert.Null(CpuTimes.ParseAggregate(content));
    }

    // --- Window calculation ---

    [Fact]
    public void Compute_ReturnsShareOfNonIdleTimeInTheWindow()
    {
        var previous = Times(user: 100, idle: 900);
        var current = Times(user: 150, idle: 950);

        Assert.Equal(50.0, CpuUsageCalculator.Compute(previous, current));
    }

    [Fact]
    public void Compute_FullyBusyWindowAfterLongIdleUptime_Reports100NotTheSinceBootAverage()
    {
        // Reproduction: days of idle uptime dominate the cumulative counters, so the
        // since-boot ratio stays near 0 % even while every core is saturated.
        var previous = Times(user: 1_000, idle: 10_000_000);
        var current = Times(user: 1_000 + 800, idle: 10_000_000);

        Assert.Equal(100.0, CpuUsageCalculator.Compute(previous, current));
    }

    [Fact]
    public void Compute_CountsIowaitAsIdle()
    {
        var previous = Times();
        var current = Times(user: 10, idle: 50, iowait: 40);

        Assert.Equal(10.0, CpuUsageCalculator.Compute(previous, current));
    }

    [Fact]
    public void Compute_CountsStealAsBusy()
    {
        var previous = Times();
        var current = Times(user: 10, steal: 10, idle: 80);

        Assert.Equal(20.0, CpuUsageCalculator.Compute(previous, current));
    }

    [Fact]
    public void Compute_NoProgress_ReturnsNull()
    {
        var sample = Times(user: 100, idle: 900);

        Assert.Null(CpuUsageCalculator.Compute(sample, sample));
    }

    [Fact]
    public void Compute_CountersWentBackwards_ReturnsNull()
    {
        var previous = Times(user: 500, idle: 5000);
        var current = Times(user: 10, idle: 100);

        Assert.Null(CpuUsageCalculator.Compute(previous, current));
    }

    [Fact]
    public void Compute_IdleGrewMoreThanTotal_ClampsToZero()
    {
        // iowait is documented to decrease occasionally; the ratio must stay in [0, 100]
        var previous = Times(user: 100, idle: 100, iowait: 50);
        var current = Times(user: 90, idle: 200, iowait: 50);

        Assert.Equal(0.0, CpuUsageCalculator.Compute(previous, current));
    }

    // --- Tracker (baseline handling between reads) ---

    [Fact]
    public void Tracker_FirstSample_HasNoWindowYet()
    {
        var tracker = new CpuUsageTracker(TimeSpan.FromSeconds(1));

        Assert.Null(tracker.Update(Times(user: 100, idle: 900), TimeSpan.Zero));
    }

    [Fact]
    public void Tracker_SecondSample_ReportsUsageOverTheWindow()
    {
        var tracker = new CpuUsageTracker(TimeSpan.FromSeconds(1));
        tracker.Update(Times(user: 100, idle: 900), TimeSpan.Zero);

        Assert.Equal(25.0, tracker.Update(Times(user: 125, idle: 975), TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Tracker_ReadInsideMinimumWindow_KeepsBaselineAndReturnsLastValue()
    {
        var tracker = new CpuUsageTracker(TimeSpan.FromSeconds(1));
        tracker.Update(Times(user: 100, idle: 900), TimeSpan.Zero);
        tracker.Update(Times(user: 150, idle: 950), TimeSpan.FromSeconds(10));     // 50 %

        // Another sensor triggers a read 200 ms later: too short to measure, so the
        // last value is repeated and the baseline is not moved.
        Assert.Equal(50.0, tracker.Update(Times(user: 151, idle: 950), TimeSpan.FromSeconds(10.2)));

        // The next window is measured from the 10 s baseline, not the 10.2 s read.
        Assert.Equal(10.0, tracker.Update(Times(user: 160, idle: 1040), TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void Tracker_ReadInsideMinimumWindowBeforeAnyValue_MeasuresTheShortWindow()
    {
        // The start-up baseline is taken when the collector is built; the first telemetry
        // read can follow within the minimum window (seen on a UNO-2484G after 0.5 s).
        // With nothing to repeat yet, the short window is better than no value.
        var tracker = new CpuUsageTracker(TimeSpan.FromSeconds(1));
        tracker.Update(Times(user: 100, idle: 900), TimeSpan.Zero);

        Assert.Equal(20.0, tracker.Update(Times(user: 110, idle: 940), TimeSpan.FromSeconds(0.5)));

        // That read became the baseline: the next full window is measured from 0.5 s
        Assert.Equal(50.0, tracker.Update(Times(user: 160, idle: 990), TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Tracker_InvalidWindow_ReportsNothingAndRestartsFromTheNewSample()
    {
        var tracker = new CpuUsageTracker(TimeSpan.FromSeconds(1));
        tracker.Update(Times(user: 500, idle: 5000), TimeSpan.Zero);

        Assert.Null(tracker.Update(Times(user: 10, idle: 100), TimeSpan.FromSeconds(10)));
        Assert.Equal(50.0, tracker.Update(Times(user: 20, idle: 110), TimeSpan.FromSeconds(20)));
    }
}
