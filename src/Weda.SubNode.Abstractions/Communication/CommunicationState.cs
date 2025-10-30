namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Communication connection state
/// </summary>
public enum CommunicationState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
