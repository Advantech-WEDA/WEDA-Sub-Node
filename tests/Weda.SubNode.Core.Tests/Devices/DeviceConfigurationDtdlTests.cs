using Shouldly;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

public class DeviceConfigurationDtdlTests
{
    private static DeviceConfiguration CreateConfig()
    {
        var config = new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                AutoGenEnabled = true,
                SubNodeType = SubNodeType.CustomDevice
            }
        };
        config.Sensors.Add(new Sensor
        {
            Name = "cpu_usage",
            SensorGroup = SensorGroup.SYS,
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = "CPU Usage" },
            Report = new SensorReport { Interval = 5000 }
        });
        return config;
    }

    [Fact]
    public void InitializeDtdl_FirstCall_GeneratesDtdlInterface()
    {
        var config = CreateConfig();

        config.InitializeDtdl();

        config.DtdlInterface.ShouldNotBeNull();
    }

    [Fact]
    public void InitializeDtdl_WhenCalledAgainAfterDtdlInterfaceSetToNull_ShouldRegenerate()
    {
        // Arrange
        var config = CreateConfig();
        config.InitializeDtdl();
        config.DtdlInterface.ShouldNotBeNull();

        // Simulate what ApplyValidatedConfigurationAsync does:
        // sets DtdlInterface = null when AutoGenEnabled changes, then calls InitializeDtdl again
        config.DtdlInterface = null;

        // Act
        config.InitializeDtdl();

        // Assert: BUG — _dtdlInitialized = true blocks regeneration, so DtdlInterface stays null
        // This test currently FAILS, proving the bug exists.
        // After fix (move _dtdlInitialized = true to after generation), this should PASS.
        config.DtdlInterface.ShouldNotBeNull();
    }
}
