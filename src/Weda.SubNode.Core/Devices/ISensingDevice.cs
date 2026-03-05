using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Protocols.ISensing;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// ISensing protocol device implementation.
/// Inherits from PubSubDeviceBase for Pub/Sub communication pattern.
/// Adds ISensing-specific functionality like sensor control and configuration.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> PubSubDeviceBase -> DeviceBase
/// </summary>
public class ISensingDevice : PubSubDeviceBase
{
    /// <summary>
    /// Initializes a new instance of ISensingDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing ISensing settings.</param>
    /// <param name="pubSub">Pub/Sub communication instance (MQTT, NATS, etc.).</param>
    public ISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPubSub pubSub)
        : base(context, configuration, CreateParser(configuration, pubSub, context.GetLogger<ISensingPubSubParser>()))
    {
    }

    /// <summary>
    /// Creates the ISensingPubSubParser for this device.
    /// </summary>
    private static ISensingPubSubParser CreateParser(
        DeviceConfiguration configuration,
        IPubSub pubSub,
        ILogger<ISensingPubSubParser> logger)
    {
        return new ISensingPubSubParser(configuration, pubSub, logger);
    }
}