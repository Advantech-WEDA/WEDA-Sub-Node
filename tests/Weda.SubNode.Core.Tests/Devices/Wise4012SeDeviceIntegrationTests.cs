using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Devices.Advantech;
using Weda.SubNode.TestBase;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Integration tests for WISE-4012SE device
/// Tests the complete device stack from Devices layer
/// </summary>
public class Wise4012SeDeviceIntegrationTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly DeviceConfiguration _configuration;
    private readonly Wise4012SeDevice _device;

    public Wise4012SeDeviceIntegrationTests()
    {
        _context = new MockApplicationContext();

        _configuration = new DeviceConfiguration
        {
            DeviceId = "wise-4012se-01",
            DeviceName = "WISE-4012SE Test Device",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>
            {
                ["BrokerUrl"] = "mqtt://localhost:1883",
                ["ClientId"] = "wise-4012se-client",
                ["MacAddress"] = "00D0C9FAC80E",
                ["Manufacturer"] = "Advantech"
            },
            Sensors = new List<Sensor>
            {
                new()
                {
                    Name = "AI0",
                    ResourceId = "ai0",
                    Dtmi = "dtmi:advantech:wise4012se:AI;1",
                    DeviceResourceId = "wise-4012se-01",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FieldName"] = "ai0"
                    },
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Unit = "mA",
                        Interval = 1000
                    }
                }
            }
        };

        _context.DeviceConfiguration = _configuration;
        _device = new Wise4012SeDevice(_context);
    }

    [Fact]
    public void Constructor_WithContext_ShouldCreateInstance()
    {
        // Assert
        Assert.NotNull(_device);
        Assert.Equal("wise-4012se-01", _device.DeviceId);
        Assert.Equal("WISE-4012SE Test Device", _device.DeviceName);
    }

    [Fact]
    public void DeviceCapabilities_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal("Advantech", _device.Configuration.DeviceCapabilities.Manufacturer);
        Assert.Equal("WISE-4012SE", _device.Configuration.DeviceCapabilities.Model);
    }

    [Fact]
    public async Task ReadTelemetryAsync_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.ReadTelemetryAsync());
    }

    public void Dispose()
    {
        _device?.Dispose();
        _context?.Dispose();
    }
}
