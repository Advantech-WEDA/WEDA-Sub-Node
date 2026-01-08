using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Hosted service that manages the lifecycle of one or more devices.
/// Initialization flow:
/// 1. SubNodeManager.InitializeAsync() - connect, register SubNode, subscribe events
/// 2. For each device: InitializeAsync() - physical connection, enrich config
/// 3. UploadDeviceConfigurationsAsync() - aggregate all configs, upload once
/// 4. For each device: StartAsync() - start background tasks
/// </summary>
internal class DeviceHostedService : IHostedService
{
    private readonly ILogger<DeviceHostedService> _logger;
    private readonly ISubNodeManager _subNodeManager;
    private readonly List<IDevice> _devices;

    /// <summary>
    /// Constructor that accepts a list of devices to manage.
    /// Supports both single device and multiple devices.
    /// </summary>
    public DeviceHostedService(
        ILogger<DeviceHostedService> logger,
        ISubNodeManager subNodeManager,
        IEnumerable<IDevice> devices)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subNodeManager = subNodeManager ?? throw new ArgumentNullException(nameof(subNodeManager));
        _devices = [.. devices ?? throw new ArgumentNullException(nameof(devices))];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_devices.Count == 0)
        {
            _logger.LogInformation("No devices configured");
            return;
        }

        _logger.LogInformation("Starting Device Hosted Service with {DeviceCount} device(s)", _devices.Count);

        // Phase 1: Initialize SubNode (shared by all devices)
        // This performs cloud connection, SubNode registration, and event subscription ONCE
        _logger.LogInformation("Initializing SubNodeManager...");
        if (!await _subNodeManager.InitializeAsync(cancellationToken))
        {
            _logger.LogError("Failed to initialize SubNodeManager. Cannot start devices.");
            return;
        }
        _logger.LogInformation("SubNodeManager initialized successfully (SubNodeId: {SubNodeId})", _subNodeManager.SubNodeId);

        // Phase 2: Initialize all devices (physical connection + enrich config)
        var initializedDevices = new List<IDevice>();
        foreach (var device in _devices)
        {
            try
            {
                _logger.LogDebug("Initializing device: {DeviceName} ({SubNodeType})",
                    device.Configuration.DeviceName,
                    device.SubNodeType);

                var initialized = await device.InitializeAsync(cancellationToken);
                if (!initialized)
                {
                    _logger.LogError("Failed to initialize device: {DeviceName}", device.Configuration.DeviceName);
                    continue;
                }

                initializedDevices.Add(device);
                _logger.LogDebug("Device initialized: {DeviceName}", device.Configuration.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing device: {DeviceName}", device.Configuration.DeviceName);
            }
        }

        if (initializedDevices.Count == 0)
        {
            _logger.LogError("No devices initialized successfully. Cannot proceed.");
            return;
        }

        // Phase 3: Aggregate and upload all device configurations
        _logger.LogInformation("Uploading aggregated configurations for {Count} devices", initializedDevices.Count);
        var configurations = new DeviceConfigurations(initializedDevices);

        if (!await _subNodeManager.UploadDeviceConfigurationsAsync(configurations, cancellationToken))
        {
            _logger.LogError("Failed to upload device configurations. Devices will start but may not be visible to cloud.");
        }
        else
        {
            _logger.LogInformation("Device configurations uploaded successfully");
        }

        // Phase 4: Start all initialized devices
        foreach (var device in initializedDevices)
        {
            try
            {
                await device.StartAsync(cancellationToken);
                _logger.LogInformation("Device started: {DeviceName} (SubNodeId: {SubNodeId})",
                    device.Configuration.DeviceName, device.SubNodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting device: {DeviceName}", device.Configuration.DeviceName);
            }
        }

        _logger.LogInformation("Device Hosted Service started successfully with {Count} device(s)", initializedDevices.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Device Hosted Service");

        foreach (var device in _devices)
        {
            try
            {
                _logger.LogInformation("Stopping device: {DeviceName}", device.Configuration.DeviceName);
                await device.StopAsync(cancellationToken);
                _logger.LogInformation("Device stopped: {DeviceName}", device.Configuration.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping device: {DeviceName}", device.Configuration.DeviceName);
            }
        }

        _logger.LogInformation("Device Hosted Service stopped");
    }
}
