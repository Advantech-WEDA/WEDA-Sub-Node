using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Protocols.ISensing;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured ISensing device with MQTT communication.
/// Connection-integrated device similar to TcpModbusDevice.
/// Simplified API using ApplicationContext.
/// </summary>
public class MqttISensingDevice : ISensingDevice
{
    /// <summary>
    /// Creates an ISensing device with MQTT communication using config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public MqttISensingDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
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

    private static IPubSub CreateMqttCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var brokerUrl = configuration.DeviceCommunication.TryGetValue("BrokerUrl", out var url)
            ? url?.ToString() ?? "mqtt://localhost:1883"
            : "mqtt://localhost:1883";

        var clientId = configuration.DeviceCommunication.TryGetValue("ClientId", out var id)
            ? id?.ToString() ?? $"mqtt-client-{Guid.NewGuid():N}"
            : $"mqtt-client-{Guid.NewGuid():N}";

        // Parse broker URL to extract host and port
        var uri = new Uri(brokerUrl);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 1883;

        // Create MQTT communication directly
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<Core.Communication.Common.CommunicationBase>();
        return new Core.Communication.Mqtt.MqttCommunication(host, port, clientId, null, logger);
    }
}
