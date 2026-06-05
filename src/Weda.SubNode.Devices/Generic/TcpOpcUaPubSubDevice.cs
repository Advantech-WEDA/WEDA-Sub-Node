using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.OpcUa;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured OPC-UA device using Pub/Sub pattern (Subscription).
/// Automatically creates OpcUaCommunication from DeviceConfiguration.
/// </summary>
public class TcpOpcUaPubSubDevice : OpcUaPubSubDevice
{
    public TcpOpcUaPubSubDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    public TcpOpcUaPubSubDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateOpcUaCommunication(context, configuration))
    {
    }

    private static OpcUaCommunication CreateOpcUaCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var comm = configuration.DeviceCommunication;
        var endpointUrl = comm.GetValueOrDefault("EndpointUrl") as string ?? "opc.tcp://localhost:4840";
        var securityMode = Enum.Parse<OpcUaSecurityMode>(
            comm.GetValueOrDefault("SecurityMode") as string ?? "None", ignoreCase: true);
        var authType = Enum.Parse<OpcUaAuthType>(
            comm.GetValueOrDefault("AuthType") as string ?? "Anonymous", ignoreCase: true);
        var username = comm.GetValueOrDefault("Username") as string;
        var password = comm.GetValueOrDefault("Password") as string;
        var certificatePath = comm.GetValueOrDefault("CertificatePath") as string;
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();
        var logger = context.GetLogger<CommunicationBase>();

        return new OpcUaCommunication(
            endpointUrl, securityMode, authType,
            username, password, certificatePath,
            connectionSettings, logger);
    }
}
