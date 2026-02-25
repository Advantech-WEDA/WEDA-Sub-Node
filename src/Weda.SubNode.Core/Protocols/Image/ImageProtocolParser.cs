using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Image;

/// <summary>
/// Protocol parser for image telemetry.
/// Supports two payload formats:
/// 1. Raw binary (byte[] = raw PNG/JPEG) → Base64 encode
/// 2. Base64 string → validate and pass through
/// </summary>
public class ImageProtocolParser(ICommunication communication) : IProtocolParser
{
    public ICommunication Communication { get; set; } = communication ?? throw new ArgumentNullException(nameof(communication));

    public object Parse(byte[] rawData)
    {
        return Convert.ToBase64String(rawData);
    }

    public byte[] Encode(object value)
    {
        return value is string base64
            ? Convert.FromBase64String(base64)
            : throw new ArgumentException($"Expected Base64 string, got {value.GetType().Name}");
    }

    public List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null)
    {
        var base64 = Convert.ToBase64String(payload);
        var resourceId = sensorMapping?.FieldToResourceId.GetValueOrDefault("image") ?? "image";

        return
        [
            new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = base64,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Metadata = new Dictionary<string, object>
                {
                    ["size"] = payload.Length,
                    ["contentType"] = DetectContentType(payload)
                }
            }
        ];
    }

    public List<TelemetryMeasure> ParseSensorData(string payload, SensorMapping? sensorMapping = null)
    {
        // Treat string as Base64, validate by decoding
        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(payload);
        }
        catch (FormatException ex)
        {
            throw new ImageProtocolException($"Invalid Base64 string: {ex.Message}", ex);
        }

        var resourceId = sensorMapping?.FieldToResourceId.GetValueOrDefault("image") ?? "image";

        return
        [
            new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = payload,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Metadata = new Dictionary<string, object>
                {
                    ["size"] = rawBytes.Length,
                    ["contentType"] = DetectContentType(rawBytes)
                }
            }
        ];
    }

    public byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures)
    {
        var measure = measures.FirstOrDefault();
        if (measure?.Value is string base64)
            return Convert.FromBase64String(base64);

        throw new ImageProtocolException("No image measure to encode");
    }

    public byte[] EncodeCommand(DeviceCommand command)
    {
        throw new NotSupportedException("Image protocol does not support commands");
    }

    internal static string DetectContentType(byte[] data)
    {
        if (data.Length < 4)
            return "application/octet-stream";

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        return "application/octet-stream";
    }
}

public class ImageProtocolException : Exception
{
    public ImageProtocolException(string message) : base(message) { }
    public ImageProtocolException(string message, Exception innerException) : base(message, innerException) { }
}