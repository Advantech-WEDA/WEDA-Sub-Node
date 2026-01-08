using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;

namespace Weda.SubNode.Cloud;

/// <summary>
/// Static factory for creating WedaNode instances
/// Provides convenient creation methods following the pattern:
/// - Cloud.Default
/// </summary>
public static class Cloud
{
    /// <summary>
    /// Create a WedaCloudService instance with default configuration
    /// Uses default implementations for DeviceAgentClient and TelemetryClient
    /// Creates a NATS connection to localhost:4222
    /// </summary>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(ILogger<WedaCloudService>? logger = null)
    {
        var natsOpts = NatsOpts.Default with { Url = "nats://localhost:4222", SerializerRegistry = WedaNatsSerializerRegistry.Default };
        var client = new NatsClient(natsOpts);
        var deviceAgentClient = new DeviceAgentClient(client, logger: null);
        var telemetryClient = new TelemetryClient(client, logger: null);

        return new WedaCloudService(client, deviceAgentClient, telemetryClient, logger: logger);
    }

    /// <summary>
    /// Create a WedaCloudService instance with custom clients
    /// </summary>
    /// <param name="client">NATS connection</param>
    /// <param name="deviceAgentClient">Custom device agent client</param>
    /// <param name="telemetryClient">Custom telemetry client</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Create(
        NatsClient client,
        IDeviceAgentClient deviceAgentClient,
        ITelemetryClient telemetryClient,
        ILogger<WedaCloudService>? logger = null)
    {
        return new WedaCloudService(client, deviceAgentClient, telemetryClient, logger: logger);
    }
}
