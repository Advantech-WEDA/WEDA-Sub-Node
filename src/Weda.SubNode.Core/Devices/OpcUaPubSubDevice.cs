using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.OpcUa;
using Weda.SubNode.Core.Protocols.OpcUa;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// OPC-UA device using Pub/Sub pattern (Subscription).
/// Server pushes value changes into SensorCache; timer samples from cache at report interval.
///
/// Use this when:
/// - You want server-push notifications (lower latency, less network traffic)
/// - The OPC-UA server supports Subscriptions
/// - Sampling rate (server-side) differs from report interval (application-side)
///
/// Inheritance: MyDevice -> TcpOpcUaPubSubDevice -> OpcUaPubSubDevice -> PubSubDeviceBase -> DeviceBase
/// </summary>
public class OpcUaPubSubDevice : PubSubDeviceBase
{
    private readonly OpcUaHybridParser _hybridParser;

    public OpcUaPubSubDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        OpcUaCommunication communication)
        : base(context, configuration, CreateParser(context, configuration, communication))
    {
        _hybridParser = (OpcUaHybridParser)_parser;

        _logger.LogDebug(
            "OpcUaPubSubDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    private static OpcUaHybridParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        OpcUaCommunication communication)
    {
        var logger = context.LoggerFactory.CreateLogger<OpcUaHybridParser>();
        return new OpcUaHybridParser(configuration, communication, logger);
    }

    /// <summary>
    /// Write a value to an OPC-UA node.
    /// </summary>
    public async Task<bool> WriteNodeAsync(
        string nodeId, object value, ushort namespaceIndex = 2,
        CancellationToken cancellationToken = default)
    {
        var command = new DeviceCommand
        {
            DeviceCmd = "WriteNode",
            Parameters = new Dictionary<string, object>
            {
                ["nodeId"] = nodeId,
                ["value"] = value,
                ["namespaceIndex"] = namespaceIndex
            }
        };

        var result = await _hybridParser.ExecuteCommandAsync(command, cancellationToken);
        if (result.IsError)
        {
            _logger.LogWarning("WriteNodeAsync failed for {NodeId}: {Error}", nodeId, result.FirstError.Description);
            return false;
        }
        return true;
    }
}
