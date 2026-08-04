using System.Text.Json;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace CfxEndpointExample;

/// <summary>
/// A CFX endpoint device that logs every captured CFX message.
/// </summary>
/// <remarks>
/// Inherits MQTT transport and CFX parsing from <see cref="MqttCfxDevice"/>. Each sensor reports one
/// CFX message type, with the message body carried as raw JSON, so this handler summarises each body
/// rather than assuming a scalar reading.
/// </remarks>
[DeviceType(Sensors.MqttCfxDevice.DeviceTypeName)]
public class MyCfxEndpointDevice : MqttCfxDevice
{
    /// <summary>
    /// Creates the device from a <c>DeviceConfigs</c> section key.
    /// </summary>
    /// <param name="context">The application context.</param>
    /// <param name="configKey">Key into the <c>DeviceConfigs</c> configuration section.</param>
    public MyCfxEndpointDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates the device from an already-resolved configuration.
    /// </summary>
    /// <remarks>
    /// This is the constructor <c>WedaApplicationBuilder.AddDevice&lt;T&gt;(sectionName)</c> activates,
    /// so it must exist for configuration-driven hosting to work.
    /// </remarks>
    /// <param name="context">The application context.</param>
    /// <param name="configuration">The resolved device configuration.</param>
    public MyCfxEndpointDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        EnableConnectionStateTracking = true;
        ConnectionStateChanged += OnConnectionStateChanged;

        // Messages arriving for types no sensor is bound to are still worth surfacing: on a shared
        // broker they reveal which endpoints and message types are live on the line.
        CfxParser.OnUnmappedMessageReceived += OnUnmappedMessage;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value is not string bodyJson)
            {
                continue;
            }

            var source = measure.Metadata?.TryGetValue("cfxSource", out var s) == true
                ? s.ToString()
                : "unknown";

            _logger.LogInformation(
                "{SensorName} from {Source}: {Summary}",
                sensor.Name,
                source,
                Summarize(bodyJson));
        }
    }

    private void OnUnmappedMessage(Weda.SubNode.Core.Protocols.Cfx.CfxEnvelope envelope) =>
        _logger.LogDebug(
            "Unmapped CFX message {MessageName} from {Source} ({Bytes} bytes of body)",
            envelope.MessageName,
            envelope.Source,
            envelope.MessageBodyJson.Length);

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEvent e)
    {
        _logger.LogInformation(
            "MQTT connection: {PreviousState} -> {CurrentState}",
            e.PreviousState,
            e.CurrentState);

        if (!string.IsNullOrEmpty(e.Reason))
        {
            _logger.LogInformation("  Reason: {Reason}", e.Reason);
        }
    }

    /// <summary>
    /// Renders a one-line summary of a CFX message body for the log.
    /// </summary>
    /// <remarks>
    /// CFX bodies range from two fields to a 24-slot magazine, so the full JSON is unreadable in a
    /// console. The most identifying scalar fields are picked out when present, and array lengths are
    /// reported rather than contents.
    /// </remarks>
    private static string Summarize(string bodyJson)
    {
        try
        {
            using var document = JsonDocument.Parse(bodyJson);
            var root = document.RootElement;
            var parts = new List<string>();

            foreach (var name in InterestingFields)
            {
                if (root.TryGetProperty(name, out var value)
                    && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    parts.Add($"{name}={value}");
                }
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    parts.Add($"{property.Name}[{property.Value.GetArrayLength()}]");
                }
            }

            return parts.Count > 0
                ? string.Join(", ", parts)
                : $"{bodyJson.Length} bytes";
        }
        catch (JsonException)
        {
            // The parser only emits bodies it has already parsed, so this is unreachable in practice;
            // fall back to a byte count rather than throwing out of a log statement.
            return $"unparseable ({bodyJson.Length} bytes)";
        }
    }

    private static readonly string[] InterestingFields =
    [
        "CFXHandle",
        "PrimaryIdentifier",
        "UnitCount",
        "Lane",
        "Result",
        "OldState",
        "NewState",
        "OldStateDuration",
        "FaultOccurrenceId",
        "TransactionID",
        "TransactionId",
        "UniqueIdentifier",
        "InspectionMethod",
        "RecipeName",
    ];
}
