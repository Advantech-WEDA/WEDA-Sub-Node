namespace Weda.SubNode.Simulators.Mqtt;

public class MqttImageSimulatorConfiguration
{
    public const string SectionName = nameof(MqttImageSimulatorConfiguration);

    /// <summary>
    /// MQTT broker host (default: localhost)
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port (default: 1883)
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// MQTT topic to publish images to
    /// </summary>
    public string Topic { get; set; } = "sensor/image/mnist";

    /// <summary>
    /// Publish interval in seconds (default: 10)
    /// </summary>
    public int IntervalSeconds { get; set; } = 10;
}