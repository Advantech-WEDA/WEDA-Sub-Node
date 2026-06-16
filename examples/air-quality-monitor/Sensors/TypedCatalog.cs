using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace AirQualityMonitor.Sensors;

public static class AirQualityDevice
{
    public const string DeviceTypeName = "air-quality";
}

public class AirQualityCommunication { }
public class AirQualityProperties { }

public class AirQualityConfiguration
    : IConfigurableDevice<AirQualityCommunication, AirQualityProperties>
{
    public static string DeviceTypeName => AirQualityDevice.DeviceTypeName;
    public static string? Description   => "HTTP poller for the MOENV (Taiwan Ministry of Environment) air-quality JSON feed.";
}

/// <summary>
/// Air-quality readings carry no per-sensor protocol parameters today — the
/// entire JSON document from the endpoint is delivered as one telemetry blob.
/// Empty Parameters POCO matches the empty <c>"Parameters": {}</c> in
/// devicecfg.json.
/// </summary>
public class AirQualitySensorParameters { }

public class AirQualitySensor : IConfigurableSensor<AirQualitySensorParameters>
{
    public static string DeviceTypeName => AirQualityDevice.DeviceTypeName;
    public static string SensorTypeName => "air-quality-json";
    public static string? Description   => "JSON-bundled air-quality readings (PM2.5 / PM10 / O3 / etc.) from one MOENV endpoint.";
}
