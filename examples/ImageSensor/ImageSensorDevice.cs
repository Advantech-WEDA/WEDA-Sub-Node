using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace ImageSensor;

/// <summary>
/// ImageSensorDevice - Receives image data via MQTT, logs Base64 preview.
/// Inherits from MqttImageDevice to get MQTT + ImageProtocolParser support.
/// </summary>
public class ImageSensorDevice : MqttImageDevice
{
    public ImageSensorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            var base64Preview = measure.Value is string s && s.Length > 40
                ? $"{s[..40]}..."
                : measure.Value?.ToString() ?? "null";

            var contentType = measure.Metadata?.GetValueOrDefault("contentType") ?? "unknown";
            var size = measure.Metadata?.GetValueOrDefault("size") ?? "?";

            _logger.LogInformation(
                "Image received: ResourceId={ResourceId}, ContentType={ContentType}, Size={Size} bytes, Base64={Preview}",
                measure.ResourceId, contentType, size, base64Preview);
        }
    }

    ~ImageSensorDevice()
    {
        DataReceived -= OnDataReceived;
    }
}
