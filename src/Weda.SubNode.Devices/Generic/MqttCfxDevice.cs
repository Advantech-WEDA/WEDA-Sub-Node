using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Weda.SubNode.Core.Protocols.Cfx;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured CFX endpoint device with MQTT communication.
/// </summary>
/// <remarks>
/// <para>
/// Builds the MQTT transport from <c>DeviceCommunication</c> so an application only has to declare
/// configuration, not wire up communication. Subscribe-only: CFX messages published by the endpoint
/// are captured passively.
/// </para>
/// <para>
/// Recognised <c>DeviceCommunication</c> keys:
/// </para>
/// <list type="table">
///   <item><term><c>BrokerUrl</c></term><description>Broker URL, for example <c>mqtt://192.168.100.19:1883</c>. Alternative to BrokerHost/BrokerPort.</description></item>
///   <item><term><c>BrokerHost</c></term><description>Broker host; defaults to <c>localhost</c>.</description></item>
///   <item><term><c>BrokerPort</c></term><description>Broker port; defaults to 1883.</description></item>
///   <item><term><c>ClientId</c></term><description>MQTT client identifier; generated when absent.</description></item>
///   <item><term><c>Username</c> / <c>Password</c></term><description>Broker credentials, when the broker requires them.</description></item>
///   <item><term><c>UseTls</c></term><description>Enable TLS for the broker connection.</description></item>
///   <item><term><c>CfxHandle</c></term><description>CFX handle to scope the subscription to one endpoint; every endpoint is matched when absent.</description></item>
///   <item><term><c>TopicRoot</c></term><description>Topic root segment; defaults to <c>CFX</c>.</description></item>
///   <item><term><c>HandleSegments</c></term><description>Handle segment count for the any-endpoint filter; defaults to 3.</description></item>
/// </list>
/// </remarks>
public class MqttCfxDevice : CfxDevice
{
    private const int DefaultPort = 1883;

    /// <summary>
    /// Creates a CFX device with MQTT communication using a configuration key.
    /// </summary>
    /// <param name="context">The application context.</param>
    /// <param name="configKey">Key into the <c>DeviceConfigs</c> configuration section.</param>
    public MqttCfxDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates a CFX device with MQTT communication from an explicit configuration.
    /// </summary>
    /// <param name="context">The application context.</param>
    /// <param name="configuration">Device configuration.</param>
    public MqttCfxDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateMqttCommunication(context, configuration))
    {
    }

    private static IPubSub CreateMqttCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configuration);

        var (host, port) = ResolveBroker(configuration);

        var clientId = Read(configuration, "ClientId") ?? $"cfx-subnode-{Guid.NewGuid():N}";
        var username = Read(configuration, "Username");
        var password = Read(configuration, "Password");
        var useTls = ReadBool(configuration, "UseTls");

        // Only attach security settings when there is something to configure: MqttCommunication
        // treats a null Security as "anonymous, plaintext".
        ConnectionSettings? settings = null;
        if (useTls || !string.IsNullOrEmpty(username))
        {
            settings = new ConnectionSettings
            {
                Security = new SecuritySettings
                {
                    UseTls = useTls,
                    Username = username,
                    Password = password,
                },
            };
        }

        var logger = context.GetLogger<CommunicationBase>();
        return new MqttCommunication(host, port, clientId, settings, logger);
    }

    private static (string Host, int Port) ResolveBroker(DeviceConfiguration configuration)
    {
        var brokerUrl = Read(configuration, "BrokerUrl");

        if (!string.IsNullOrWhiteSpace(brokerUrl))
        {
            if (!Uri.TryCreate(brokerUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"CFX device '{configuration.DeviceName}' has an unparseable BrokerUrl '{brokerUrl}'. "
                    + "Expected a form such as 'mqtt://192.168.100.19:1883'.");
            }

            return (uri.Host, uri.Port > 0 ? uri.Port : DefaultPort);
        }

        var host = Read(configuration, "BrokerHost") ?? "localhost";
        var rawPort = Read(configuration, "BrokerPort");

        if (string.IsNullOrWhiteSpace(rawPort))
        {
            return (host, DefaultPort);
        }

        if (!int.TryParse(rawPort, out var port))
        {
            throw new InvalidOperationException(
                $"CFX device '{configuration.DeviceName}' has a non-numeric BrokerPort '{rawPort}'.");
        }

        return (host, port);
    }

    private static string? Read(DeviceConfiguration configuration, string key) =>
        configuration.DeviceCommunication.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private static bool ReadBool(DeviceConfiguration configuration, string key)
    {
        var raw = Read(configuration, key);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!bool.TryParse(raw, out var parsed))
        {
            throw new InvalidOperationException(
                $"CFX configuration key '{key}' must be a boolean but was '{raw}'.");
        }

        return parsed;
    }
}
