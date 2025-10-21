namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device lifecycle status
/// </summary>
public enum DeviceStatus
{
    Initializing,
    Ready,
    Running,
    Paused,
    Error,
    Stopped
}
