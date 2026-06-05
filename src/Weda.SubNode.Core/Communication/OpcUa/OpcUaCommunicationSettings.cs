namespace Weda.SubNode.Core.Communication.OpcUa;

/// <summary>
/// OPC-UA communication settings.
/// Maps directly from DeviceConfiguration.DeviceCommunication dictionary.
/// </summary>
public class OpcUaCommunicationSettings
{
    /// <summary>
    /// OPC-UA server endpoint URL. Default: "opc.tcp://localhost:4840"
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    /// <summary>
    /// Security mode: None, Sign, SignAndEncrypt. Default: "None"
    /// </summary>
    public string SecurityMode { get; set; } = "None";

    /// <summary>
    /// Authentication type: Anonymous, UserPassword, Certificate. Default: "Anonymous"
    /// </summary>
    public string AuthType { get; set; } = "Anonymous";

    /// <summary>
    /// Username for UserPassword authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for UserPassword authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Path to certificate file for Certificate authentication
    /// </summary>
    public string? CertificatePath { get; set; }
}
