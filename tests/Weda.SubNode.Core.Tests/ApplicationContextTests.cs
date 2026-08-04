using Microsoft.Extensions.Configuration;
using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Tcp;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Host.Context;
using Weda.SubNode.TestBase.Builders;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Tests for WedaApplicationContext
/// </summary>
public class ApplicationContextTests
{
    [Fact]
    public void Constructor_Should_CreateWithDefaults()
    {
        // Act
        using var context = new WedaApplicationContext();

        // Assert
        context.ShouldNotBeNull();
        context.CloudService.ShouldNotBeNull();
        context.LoggerFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_Should_AcceptCustomOptions()
    {
        // Arrange & Act
        using var context = new WedaApplicationContext(options =>
        {
            options.ConnectionOptions = new ConnectionOptions
            {
                MaxRetryAttempts = 5,
                RetryDelayMs = 2000
            };
        });

        // Assert
        context.ConnectionOptions.MaxRetryAttempts.ShouldBe(5);
        context.ConnectionOptions.RetryDelayMs.ShouldBe(2000);
    }

    [Fact]
    public void CreateModbusDevice_Should_CreateDevice()
    {
        // Arrange
        var host = "localhost";
        var port = 502;
        using var context = new WedaApplicationContext();
        var config = DeviceConfigurationBuilder.Default()
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = host,
                ["Port"] = port,
                ["SlaveId"] = 1
            })
            .Build();
        var communication = new TcpCommunication(host, port);

        // Act
        var device = new ModbusDevice(context, config, communication);

        // Assert
        device.ShouldNotBeNull();
        device.SubNodeType.ShouldBe(SubNodeType.AdamEthernet);
    }

    [Fact]
    public void CreateTcpCommunication_Should_CreateCommunication()
    {
        // Arrange
        using var context = new WedaApplicationContext();

        // Act
        var communication = new TcpCommunication("localhost", 502);

        // Assert
        communication.ShouldNotBeNull();
    }

    [Fact]
    public void GetLogger_Should_ReturnTypedLogger()
    {
        // Arrange
        using var context = new WedaApplicationContext();

        // Act
        var logger = context.GetLogger<ApplicationContextTests>();

        // Assert
        logger.ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_Should_NotThrow()
    {
        // Arrange
        var context = new WedaApplicationContext();

        // Act & Assert
        Should.NotThrow(() => context.Dispose());
    }

    [Fact]
    public void Constructor_Should_FailFastWhenDeviceConfigKeyExceedsMaxLength()
    {
        // The DeviceConfigs key becomes the DTMI namespace segment
        // (dtmi:sub:<device-cfg-key>:...), so its length is capped at
        // DtdlGenerator.MaxDeviceKeyLength.
        var longKey = new string('D', Abstractions.DigitalTwin.DtdlGenerator.MaxDeviceKeyLength + 1);
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"DeviceConfig:DeviceConfigs:{longKey}:Enabled"] = "true"
            })
            .Build();

        var ex = Should.Throw<InvalidOperationException>(() =>
            new WedaApplicationContext(options => options.Configuration = configuration));

        ex.Message.ShouldContain(longKey);
        ex.Message.ShouldContain(Abstractions.DigitalTwin.DtdlGenerator.MaxDeviceKeyLength.ToString());
    }

    [Fact]
    public void Constructor_Should_AcceptDeviceConfigKeyAtMaxLength()
    {
        // Arrange
        var maxKey = new string('D', Abstractions.DigitalTwin.DtdlGenerator.MaxDeviceKeyLength);
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"DeviceConfig:DeviceConfigs:{maxKey}:Enabled"] = "true",
                [$"DeviceConfig:DeviceConfigs:{maxKey}:Dtdl:AutoGenEnabled"] = "true"
            })
            .Build();

        // Act
        using var context = new WedaApplicationContext(options => options.Configuration = configuration);

        // Assert
        context.DeviceConfigs.Keys.ShouldContain(maxKey);
    }
}
