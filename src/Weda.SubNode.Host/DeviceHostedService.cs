using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Hosted service that manages the lifecycle of all registered devices
/// Automatically initializes, starts, and stops devices
/// </summary>
internal class DeviceHostedService : IHostedService
{
    private readonly ILogger<DeviceHostedService> _logger;
    private readonly IReadOnlyList<DeviceConfiguration> _deviceConfigurations;
    private readonly IWedaApplicationContext _context;
    private readonly List<IDevice> _devices = new();

    public DeviceHostedService(
        ILogger<DeviceHostedService> logger,
        IReadOnlyList<DeviceConfiguration> deviceConfigurations,
        IWedaApplicationContext context)
    {
        _logger = logger;
        _deviceConfigurations = deviceConfigurations;
        _context = context;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Device Hosted Service");
        _logger.LogInformation("Found {DeviceCount} device configuration(s)", _deviceConfigurations.Count);

        foreach (var config in _deviceConfigurations)
        {
            try
            {
                _logger.LogInformation("Initializing device: {DeviceName} ({DeviceType})",
                    config.DeviceName, config.DeviceType);

                var device = CreateDevice(config);
                _devices.Add(device);

                // Initialize device (connect + register)
                var initialized = await device.InitializeAsync(cancellationToken);
                if (!initialized)
                {
                    _logger.LogError("Failed to initialize device: {DeviceName}", config.DeviceName);
                    continue;
                }

                // Start background tasks (telemetry, health reporting)
                await device.StartAsync(cancellationToken);

                _logger.LogInformation("Device started successfully: {DeviceName} (DeviceId: {DeviceId})",
                    config.DeviceName, device.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting device: {DeviceName}", config.DeviceName);
            }
        }

        _logger.LogInformation("Device Hosted Service started with {DeviceCount} active device(s)", _devices.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Device Hosted Service");

        foreach (var device in _devices)
        {
            try
            {
                _logger.LogInformation("Stopping device: {DeviceId}", device.DeviceId);
                await device.StopAsync(cancellationToken);
                _logger.LogInformation("Device stopped: {DeviceId}", device.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping device: {DeviceId}", device.DeviceId);
            }
        }

        _logger.LogInformation("Device Hosted Service stopped");
    }

    /// <summary>
    /// Create a device instance based on the configuration
    /// Currently supports Modbus devices, can be extended for other protocols
    /// </summary>
    private IDevice CreateDevice(DeviceConfiguration config)
    {
        // Determine protocol type from Properties or Communication settings
        var protocolType = config.Properties.GetValueOrDefault("ProtocolType") as string
            ?? config.Communication.GetValueOrDefault("ProtocolType") as string
            ?? "modbus"; // Default to Modbus

        return protocolType.ToLowerInvariant() switch
        {
            "modbus" => CreateModbusDevice(config),
            _ => throw new NotSupportedException($"Protocol type '{protocolType}' is not supported")
        };
    }

    private IDevice CreateModbusDevice(DeviceConfiguration config)
    {
        // Create TCP communication
        var host = config.Communication.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        var port = config.Communication.TryGetValue("Port", out var p) ? Convert.ToInt32(p) : 502;
        var logger = _context.GetLogger<CommunicationBase>();
        var communication = new TcpCommunication(host, port, null, logger);

        // Create Modbus device with ApplicationContext
        return new ModbusDevice(_context, config, communication);
    }
}
