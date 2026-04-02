using SystemAgentExample.Models;

using Xunit;

namespace SystemAgentDevice.Tests;

public class GpioMetricsLookupTests
{
    [Fact]
    public void PinStateDetails_LookupByName_ReturnsValue()
    {
        var metrics = new GpioMetrics
        {
            IsSupported = true,
            PinNames = ["DI_0", "DI_1", "DO_0"],
            PinStateDetails = new Dictionary<string, int>
            {
                ["DI_0"] = 1,
                ["DI_1"] = 0,
                ["DO_0"] = 1
            },
            PinIndexToName = new Dictionary<int, string>
            {
                [0] = "DI_0",
                [1] = "DI_1",
                [2] = "DO_0"
            }
        };

        // Direct name lookup
        Assert.True(metrics.PinStateDetails.TryGetValue("DI_0", out var val));
        Assert.Equal(1, val);
    }

    [Fact]
    public void PinIndexToName_LookupByIndex_ResolvesToName()
    {
        var metrics = new GpioMetrics
        {
            IsSupported = true,
            PinNames = ["DI_0", "DI_1", "DO_0"],
            PinStateDetails = new Dictionary<string, int>
            {
                ["DI_0"] = 1,
                ["DI_1"] = 0,
                ["DO_0"] = 1
            },
            PinIndexToName = new Dictionary<int, string>
            {
                [0] = "DI_0",
                [1] = "DI_1",
                [2] = "DO_0"
            }
        };

        // Index-based lookup: "0" -> DI_0
        var pinId = "0";
        Assert.True(int.TryParse(pinId, out var idx));
        Assert.True(metrics.PinIndexToName.TryGetValue(idx, out var resolvedName));
        Assert.Equal("DI_0", resolvedName);
        Assert.True(metrics.PinStateDetails.TryGetValue(resolvedName, out var val));
        Assert.Equal(1, val);
    }

    [Fact]
    public void PinIndexToName_InvalidIndex_ReturnsFalse()
    {
        var metrics = new GpioMetrics
        {
            IsSupported = true,
            PinNames = ["DI_0"],
            PinStateDetails = new Dictionary<string, int> { ["DI_0"] = 0 },
            PinIndexToName = new Dictionary<int, string> { [0] = "DI_0" }
        };

        Assert.False(metrics.PinIndexToName.TryGetValue(99, out _));
    }

    [Fact]
    public void PinLookup_NameFirst_ThenFallbackToIndex()
    {
        var metrics = new GpioMetrics
        {
            IsSupported = true,
            PinNames = ["DI_0", "DI_1"],
            PinStateDetails = new Dictionary<string, int>
            {
                ["DI_0"] = 1,
                ["DI_1"] = 0
            },
            PinIndexToName = new Dictionary<int, string>
            {
                [0] = "DI_0",
                [1] = "DI_1"
            }
        };

        // Simulate the parser logic: try name first, then index
        var pinIdStr = "1"; // This is a numeric ID

        // Name lookup: "1" is not a pin name
        var foundByName = metrics.PinStateDetails.TryGetValue(pinIdStr, out _);
        Assert.False(foundByName);

        // Index lookup: parse "1" -> index 1 -> "DI_1"
        Assert.True(int.TryParse(pinIdStr, out var pinIndex));
        Assert.True(metrics.PinIndexToName.TryGetValue(pinIndex, out var resolved));
        Assert.Equal("DI_1", resolved);
        Assert.True(metrics.PinStateDetails.TryGetValue(resolved, out var state));
        Assert.Equal(0, state);
    }
}
