using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// CFX endpoint device implementation.
/// </summary>
/// <remarks>
/// <para>
/// Represents one IPC-CFX endpoint (a machine on an SMT line) whose messages reach the SubNode over
/// a Pub/Sub transport. Subscribe-only: CFX messages are captured passively as they are published.
/// </para>
/// <para>
/// Architecture: Device → Parser → Communication.
/// Inheritance: <c>MyCfxDevice</c> → <see cref="CfxDevice"/> → <see cref="PubSubDeviceBase"/> → <c>DeviceBase</c>.
/// </para>
/// </remarks>
public class CfxDevice : PubSubDeviceBase
{
    /// <summary>
    /// Initializes a new instance of <see cref="CfxDevice"/>.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">
    /// Device configuration; each sensor binds to one CFX message name via its <c>MessageName</c>
    /// parameter.
    /// </param>
    /// <param name="pubSub">Pub/Sub communication instance (MQTT today, AMQP 1.0 in future).</param>
    public CfxDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPubSub pubSub)
        : base(context, configuration, CreateParser(configuration, pubSub, context.GetLogger<CfxPubSubParser>()))
    {
    }

    /// <summary>
    /// The CFX parser backing this device, for subscribing to unmapped-message notifications.
    /// </summary>
    protected CfxPubSubParser CfxParser => (CfxPubSubParser)_parser;

    /// <summary>
    /// CFX messages are discrete process events, so each one is reported exactly once as it arrives.
    /// </summary>
    /// <remarks>
    /// Interval sampling would both duplicate and drop them: a cached event is re-reported on every
    /// tick until replaced, and two messages of one type inside a single interval collapse to the
    /// last. Reporting on arrival also preserves the publisher's own <c>TimeStamp</c> as the
    /// authoritative time for each event.
    /// </remarks>
    protected override bool UseEventDrivenTelemetry => true;

    private static CfxPubSubParser CreateParser(
        DeviceConfiguration configuration,
        IPubSub pubSub,
        ILogger<CfxPubSubParser> logger)
    {
        return new CfxPubSubParser(configuration, pubSub, logger);
    }
}
