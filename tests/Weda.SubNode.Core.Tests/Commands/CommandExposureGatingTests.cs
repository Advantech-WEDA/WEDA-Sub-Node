using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands;

using Xunit;

namespace Weda.SubNode.Core.Tests.Commands;

/// <summary>
/// Verifies capability-gated command exposure: commands whose handler
/// carries <c>[RequiresDeviceCapability]</c> only appear in
/// <see cref="CommandRegistry.GetDescriptors"/> when a registered device class
/// implements the capability. Dispatch registration is never gated.
/// </summary>
public class CommandExposureGatingTests
{
    /// <summary>Built-in I/O commands, each gated on a device capability.</summary>
    private static readonly string[] CapabilityGatedCommands =
        ["di.get", "do.get", "do.set", "ai.get", "ao.get", "ao.set"];

    /// <summary>Stand-in for a device class that implements DI read.</summary>
    private interface IFakeDigitalInputDevice : IDigitalInputReadable;

    /// <summary>Stand-in for a device class with no I/O capabilities
    /// (e.g. System Agent's LocalSystemAgentDevice).</summary>
    private class FakeCapabilityFreeDevice;

    private static CommandRegistry CreateScannedRegistry()
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);
        return registry;
    }

    [Fact]
    public void Without_device_class_context_all_commands_are_exposed()
    {
        var registry = CreateScannedRegistry();

        var names = registry.GetDescriptors().Select(d => d.Name).ToHashSet();

        foreach (var cmd in CapabilityGatedCommands)
            Assert.Contains(cmd, names);
    }

    [Fact]
    public void Capability_free_device_hides_all_io_commands_but_keeps_reports()
    {
        var registry = CreateScannedRegistry();
        registry.SetDeviceClasses([typeof(FakeCapabilityFreeDevice)]);

        var names = registry.GetDescriptors().Select(d => d.Name).ToHashSet();

        foreach (var cmd in CapabilityGatedCommands)
            Assert.DoesNotContain(cmd, names);

        // Sensor-based commands carry no capability requirement.
        Assert.Contains("report.data", names);
        Assert.Contains("report.historical", names);
    }

    [Fact]
    public void Digital_input_capable_device_exposes_di_get_only()
    {
        var registry = CreateScannedRegistry();
        registry.SetDeviceClasses([typeof(IFakeDigitalInputDevice)]);

        var names = registry.GetDescriptors().Select(d => d.Name).ToHashSet();

        Assert.Contains("di.get", names);
        foreach (var cmd in CapabilityGatedCommands.Where(c => c != "di.get"))
            Assert.DoesNotContain(cmd, names);
    }

    [Fact]
    public void Gating_only_affects_exposure_not_dispatch()
    {
        var registry = CreateScannedRegistry();
        registry.SetDeviceClasses([typeof(FakeCapabilityFreeDevice)]);

        // do.set is not exposed…
        Assert.DoesNotContain(registry.GetDescriptors(), d => d.Name == "do.set");

        // …but it is still registered and dispatchable; the handler's own
        // runtime device lookup reports the missing capability.
        Assert.NotNull(registry.GetRegistration("do.set"));
    }

    [Fact]
    public void Scan_captures_required_capabilities_from_handler_attribute()
    {
        var registry = CreateScannedRegistry();

        var diGet = registry.GetRegistration("di.get");
        Assert.NotNull(diGet);
        Assert.Equal([typeof(IDigitalInputReadable)], diGet.RequiredCapabilities);

        var reportData = registry.GetRegistration("report.data");
        Assert.NotNull(reportData);
        Assert.Empty(reportData.RequiredCapabilities);
    }
}
