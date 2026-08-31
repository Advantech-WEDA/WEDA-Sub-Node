using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Unit tests for BackgroundTaskPeriods defaults and bounds (US-47107 AC-1).
/// </summary>
public class BackgroundTaskPeriodsTests
{
    [Fact]
    public void ReportConfiguration_Should_Default_To_24Hours()
    {
        var periods = new BackgroundTaskPeriods();

        periods.ReportConfiguration.ShouldBe(86_400_000);
    }

    [Fact]
    public void MinReportConfigurationPeriod_Should_Be_1Minute()
    {
        BackgroundTaskPeriods.MinReportConfigurationPeriod.ShouldBe(60_000);
    }

    [Fact]
    public void MaxReportConfigurationPeriod_Should_Be_7Days()
    {
        BackgroundTaskPeriods.MaxReportConfigurationPeriod.ShouldBe(604_800_000);
    }
}
