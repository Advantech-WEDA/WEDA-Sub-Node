using System.Net.Sockets;

using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Devices;
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
            .WithDeviceType(DeviceType.AdamEthernet)
            .WithCommunication(new Dictionary<string, object>
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
        device.DeviceType.ShouldBe(DeviceType.AdamEthernet);
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
}
