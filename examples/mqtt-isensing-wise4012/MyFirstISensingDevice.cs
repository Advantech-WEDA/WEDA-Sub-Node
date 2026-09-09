using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace Wise4012ISensingExample;

/// <summary>
/// MyFirstISensingDevice - A custom ISensing device implementation
/// Inherits from MqttISensingDevice to get MQTT + ISensing protocol support with automatic communication setup
/// </summary>
[DeviceType(Sensors.MqttISensingDevice.DeviceTypeName)]
public class MyFirstISensingDevice : MqttISensingDevice
{
    /// <summary>
    /// Creates MyFirstISensingDevice using ApplicationContext and config key.
    /// Configuration is retrieved from context.DeviceConfigs[configKey].
    /// MQTT connection and ISensing protocol are automatically configured.
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public MyFirstISensingDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        Initialize();
    }

    /// <summary>
    /// Creates MyFirstISensingDevice from an already-resolved <see cref="DeviceConfiguration"/>.
    /// This is the overload the host's typed dispatch activates; without it device construction
    /// fails at startup with MissingMethodException.
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configuration">The resolved device configuration</param>
    public MyFirstISensingDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        Initialize();
    }

    private void Initialize()
    {
        // Subscribe to DataReceived event to process telemetry
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        // Subscribe to ConnectionStateChanged event to monitor MQTT connection
        EnableConnectionStateTracking = true;
        ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Prints all sensor values from ISensing JSON messages
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyFirstISensingDevice: Data received, Count={Count}", e.Data.Count);

        // Print all sensor values based on configuration
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value}",
                    sensor.Name,
                    measure.Value);
            }
        }
    }

    /// <summary>
    /// Event handler for MQTT connection state changes
    /// Logs connection/disconnection events
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEvent e)
    {
        _logger.LogInformation("MQTT Connection: {PreviousState} -> {CurrentState}",
            e.PreviousState,
            e.CurrentState);

        if (!string.IsNullOrEmpty(e.Reason))
        {
            _logger.LogInformation("  Reason: {Reason}", e.Reason);
        }
    }

    ~MyFirstISensingDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
        ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
