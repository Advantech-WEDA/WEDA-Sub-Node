using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Weda.SubNode.Core.Devices;

using VisionObjectDetection.Protocols;

namespace VisionObjectDetection.Devices;

/// <summary>
/// SubNode device that ingests the Advantech YOLO object-detection demo container.
///
/// <para>Subscribes over MQTT to <c>advantech/&lt;DEVICE_ID&gt;/vision/*</c>, maps the
/// 1&#160;Hz detection stream into scalar sensors via <see cref="VisionDetectionParser"/>,
/// and reports them to WedaNode. Inherits the full Pub/Sub telemetry pipeline
/// (SensorCache &#8594; interval sampling &#8594; batch send) from
/// <see cref="PubSubDeviceBase"/> — the same pattern as the SDK's MqttImageDevice.</para>
/// </summary>
[DeviceType(DeviceTypeName)]
public sealed class VisionDetectionDevice : PubSubDeviceBase
{
    /// <summary>DTDL device-type identifier used by the host loader.</summary>
    public const string DeviceTypeName = "mqtt-vision";

    private const string DefaultBrokerUrl = "mqtt://localhost:1883";

    /// <summary>Builder entry point: <c>AddDevice&lt;VisionDetectionDevice&gt;("VisionDetectionConfig")</c>.</summary>
    public VisionDetectionDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    public VisionDetectionDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private static VisionDetectionParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var brokerUrl = configuration.DeviceCommunication.TryGetValue("BrokerUrl", out var url)
            && url?.ToString() is { Length: > 0 } configuredUrl
                ? configuredUrl
                : DefaultBrokerUrl;

        var clientId = configuration.DeviceCommunication.TryGetValue("ClientId", out var id)
            && id?.ToString() is { Length: > 0 } configuredId
                ? configuredId
                : $"vision-subnode-{Guid.NewGuid():N}";

        var uri = new Uri(brokerUrl);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 1883;

        var commLogger = context.GetLogger<CommunicationBase>();
        var communication = new MqttCommunication(host, port, clientId, null, commLogger);

        var parserLogger = context.GetLogger<VisionDetectionParser>();
        return new VisionDetectionParser(configuration, communication, parserLogger);
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            _logger.LogInformation(
                "Vision telemetry: {Field} = {Value} (device {DeviceId})",
                measure.Metadata?.GetValueOrDefault("field") ?? measure.ResourceId,
                measure.Value,
                measure.Metadata?.GetValueOrDefault("deviceId") ?? "unknown");
        }
    }
}
