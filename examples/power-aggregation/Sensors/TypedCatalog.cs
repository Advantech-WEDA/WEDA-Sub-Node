using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Sensors;

// power-aggregation has THREE device entries in devicecfg.json:
//   - CurrentSensor / VoltageSensor : TcpModbusDevice → already typed via the
//     SDK's [DeviceType("tcp-modbus")] on TcpModbusDevice; no work here.
//   - PowerAggregator : AggregatorDevice → declared below.

public static class AggregatorDevice
{
    public const string DeviceTypeName = "aggregator";
}

public class AggregatorCommunication { }
public class AggregatorProperties { }

public class AggregatorConfiguration
    : IConfigurableDevice<AggregatorCommunication, AggregatorProperties>
{
    public static string DeviceTypeName => AggregatorDevice.DeviceTypeName;
    public static string? Description   => "Pub/Sub aggregator computing a derived measurement (e.g. P = V × I) from upstream sensors.";
}

public class PowerChannelParameters
{
    [Required, JsonPropertyName("voltageSource")]
    public string VoltageSource { get; init; } = string.Empty;

    [Required, JsonPropertyName("currentSource")]
    public string CurrentSource { get; init; } = string.Empty;

    [Required, JsonPropertyName("dataType")]
    public string DataType { get; init; } = string.Empty;
}

public class PowerChannelSensor : IConfigurableSensor<PowerChannelParameters>
{
    public static string DeviceTypeName => AggregatorDevice.DeviceTypeName;
    public static string SensorTypeName => "power-channel";
    public static string? Description   => "Derived power (P = V × I) channel referencing upstream voltage + current sensors by ResourceId path.";
}
