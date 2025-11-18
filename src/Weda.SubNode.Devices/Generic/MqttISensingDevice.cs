using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured ISensing device with MQTT communication.
/// Connection-integrated device similar to TcpModbusDevice.
/// Simplified API using ApplicationContext.
/// </summary>
public class MqttISensingDevice : Core.Devices.ISensingDevice
{
    /// <summary>
    /// Creates an ISensing device with MQTT communication using ApplicationContext only.
    /// Automatically retrieves configuration from context and creates MQTT communication.
    /// </summary>
    public MqttISensingDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    /// <summary>
    /// Creates an ISensing device with ApplicationContext and explicit configuration.
    /// Automatically creates MQTT communication from configuration (BrokerUrl, ClientId).
    /// </summary>
    public MqttISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateMqttCommunication(context, configuration))
    {
    }

    private static IMessageBroker CreateMqttCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var brokerUrl = configuration.Communication.TryGetValue("BrokerUrl", out var url)
            ? url?.ToString() ?? "mqtt://localhost:1883"
            : "mqtt://localhost:1883";

        var clientId = configuration.Communication.TryGetValue("ClientId", out var id)
            ? id?.ToString() ?? $"mqtt-client-{Guid.NewGuid():N}"
            : $"mqtt-client-{Guid.NewGuid():N}";

        // Parse broker URL to extract host and port
        var uri = new Uri(brokerUrl);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 1883;

        // Create MQTT communication directly
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<Core.Communication.CommunicationBase>();
        return new Core.Communication.MqttCommunication(host, port, clientId, null, logger);
    }
}
