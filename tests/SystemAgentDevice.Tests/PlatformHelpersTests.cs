using SystemAgentExample.Communication.Utilities;

using Xunit;

namespace SystemAgentDevice.Tests;

public class PlatformHelpersTests
{
    // --- NormalizeDriveName ---

    [Theory]
    [InlineData("C:", "c")]
    [InlineData("D:", "d")]
    [InlineData("C:\\", "c")]
    [InlineData("/dev/sda1", "dev_sda1")]
    [InlineData("/dev/sdb", "dev_sdb")]
    [InlineData("/", "root")]
    [InlineData("", "root")]
    [InlineData("/Volumes/Data", "Volumes_Data")]
    public void NormalizeDriveName_ReturnsExpected(string input, string expected)
    {
        var result = PlatformHelpers.NormalizeDriveName(input);
        Assert.Equal(expected, result);
    }

    // --- SanitizeInterfaceName ---

    [Theory]
    [InlineData("eth0", "eth0")]
    [InlineData("Intel(R) Ethernet Connection (17) I219-V", "intel_r_ethernet_connection_17_i219_v")]
    [InlineData("Hyper-V Virtual Ethernet Adapter", "hyper_v_virtual_ethernet_adapter")]
    [InlineData("Wi-Fi", "wi_fi")]
    [InlineData("Ethernet 1", "ethernet_1")]
    public void SanitizeInterfaceName_NormalInput_ReturnsNormalized(string description, string expected)
    {
        var result = PlatformHelpers.SanitizeInterfaceName(description);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeInterfaceName_EmptyDescription_FallsBackToFriendlyName()
    {
        var result = PlatformHelpers.SanitizeInterfaceName("()", "Ethernet 1");
        Assert.Equal("ethernet_1", result);
    }

    [Fact]
    public void SanitizeInterfaceName_AllSpecialChars_FallsBackToFriendlyName()
    {
        var result = PlatformHelpers.SanitizeInterfaceName("@#$%", "My-NIC");
        Assert.Equal("my_nic", result);
    }

    [Fact]
    public void SanitizeInterfaceName_BothEmpty_FallsBackToOriginalReplace()
    {
        // Edge case: description is non-ASCII only, no friendlyName
        var result = PlatformHelpers.SanitizeInterfaceName("---", null);
        Assert.Equal("___", result);
    }

    [Fact]
    public void SanitizeInterfaceName_NoFriendlyName_UsesDescriptionOnly()
    {
        var result = PlatformHelpers.SanitizeInterfaceName("Intel NIC");
        Assert.Equal("intel_nic", result);
    }
}
