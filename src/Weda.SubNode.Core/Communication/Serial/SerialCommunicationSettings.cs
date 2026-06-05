using System.IO.Ports;

namespace Weda.SubNode.Core.Communication.Serial;

public class SerialCommunicationSettings
{
    /// <summary>
    /// Serial port name (e.g. "COM1" on Windows, "/dev/ttyUSB0" on linux)
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// Baud rate. Default: 9600
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Data bits. Default: 8
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Stop bits. Default: One
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// Parity. Default: None
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// Read timeout in milliseconds. Default: 1000ms 
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1_000;

    /// <summary>
    /// Write timeout in milliseconds. Default: 2000ms
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 2_000;
}