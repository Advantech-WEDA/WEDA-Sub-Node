using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.OpcUa;
using Weda.SubNode.Core.Protocols.OpcUa;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// OPC-UA device using Request-Response pattern (polling).
/// Timer-driven: each interval calls ReadNodesAsync to batch-read all sensor values.
///
/// Use this when:
/// - You need deterministic polling intervals
/// - The OPC-UA server does not support Subscriptions well
/// - You want the same behavior as Modbus TCP devices
///
/// Inheritance: MyDevice -> TcpOpcUaRequestResponseDevice -> OpcUaRequestResponseDevice -> RequestResponseDeviceBase -> DeviceBase
/// </summary>
public class OpcUaRequestResponseDevice : RequestResponseDeviceBase
{
    private readonly OpcUaHybridParser _hybridParser;

    public OpcUaRequestResponseDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        OpcUaCommunication communication)
        : base(context, configuration, CreateParser(context, configuration, communication))
    {
        _hybridParser = (OpcUaHybridParser)_parser;

        _logger.LogDebug(
            "OpcUaRequestResponseDevice initialized ({SensorCount} sensors)",
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
