namespace Weda.SubNode.Core.Communication.Tcp;

/// <summary>
/// TCP communication settings.
/// Maps directly from DeviceConfiguration.Communication dictionary.
/// </summary>
public class TcpCommunicationSettings
{
    /// <summary>
    /// Host address (IP or hostname). Default: "localhost"
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// TCP port number. Default: 502 (standard Modbus TCP port)
    /// </summary>
    public int Port { get; set; } = 502;
}