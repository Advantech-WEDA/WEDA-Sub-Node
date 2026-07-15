using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    [Required]
    [Display(Name = "Host")]
    [Description("TCP host name or IP address.")]
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// TCP port number. Default: 502 (standard Modbus TCP port)
    /// </summary>
    [Range(1, 65535)]
    [Display(Name = "Port")]
    [Description("TCP port.")]
    [JsonPropertyName("port")]
    public int Port { get; set; } = 502;
}