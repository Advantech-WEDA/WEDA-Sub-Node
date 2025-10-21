using Weda.SubNode.Abstractions.Cloud.Clients.Common;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;

/// <summary>
/// Health report request
/// </summary>
public class HealthReportMessage : Message<DeviceHealth>
{
    /// <summary>
    /// Create a new health report request with auto-generated audit fields
    /// </summary>
    public static HealthReportMessage Create(DeviceHealth data)
    {
        return Create<HealthReportMessage>(data);
    }
}
