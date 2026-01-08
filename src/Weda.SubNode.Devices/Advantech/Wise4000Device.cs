using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Devices.Generic;

namespace Weda.SubNode.Devices.Advantech;

/// <summary>
/// WISE-4012SE device implementation.
/// 4-channel analog input module with MQTT ISensing protocol support.
///
/// Features:
/// - 4x Analog Input channels (AI0-AI3)
/// - MQTT communication with ISensing JSON protocol
/// - Temperature and humidity monitoring
/// - Pub/Sub architecture for real-time data
/// </summary>
public class Wise4000Device : MqttISensingDevice
{
    /// <summary>
    /// Creates a WISE-4012SE device with ApplicationContext and config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public Wise4000Device(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
    }

    /// <summary>
    /// Creates a WISE-4012SE device with ApplicationContext and explicit configuration.
    /// </summary>
    public Wise4000Device(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
    }
}
