using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Image;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured Image device with MQTT communication.
/// Subscribes to MQTT topic, receives raw image bytes,
/// converts to Base64 via ImageProtocolParser pipeline.
/// </summary>
public class MqttImageDevice : PubSubDeviceBase
{
    public MqttImageDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    public MqttImageDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
    }

    private static ImagePubSubParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var brokerUrl = configuration.DeviceCommunication.TryGetValue("BrokerUrl", out var url)
            ? url?.ToString() ?? "mqtt://localhost:1883"
            : "mqtt://localhost:1883";

        var clientId = configuration.DeviceCommunication.TryGetValue("ClientId", out var id)
            ? id?.ToString() ?? $"mqtt-image-{Guid.NewGuid():N}"
            : $"mqtt-image-{Guid.NewGuid():N}";

        var uri = new Uri(brokerUrl);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 1883;

        var commLogger = context.GetLogger<Core.Communication.Common.CommunicationBase>();
        var communication = new Core.Communication.Mqtt.MqttCommunication(host, port, clientId, null, commLogger);

        var parserLogger = context.GetLogger<ImagePubSubParser>();
        return new ImagePubSubParser(configuration, communication, parserLogger);
    }
}
