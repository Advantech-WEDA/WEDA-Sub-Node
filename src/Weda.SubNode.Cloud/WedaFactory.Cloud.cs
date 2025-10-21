using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Core.Cloud;

namespace Weda.SubNode.Cloud;

/// <summary>
/// WedaFactory for cloud services.
/// Provides static factory methods for creating cloud service instances.
/// </summary>
public static partial class WedaFactory
{
    /// <summary>
    /// Cloud service factories.
    /// </summary>
    public static class Cloud
    {
        /// <summary>
        /// Default cloud service (NATS-based EdgeSync cloud service).
        /// Creates a NATS connection to localhost:4222.
        /// Note: Logger will be null. Use UseDefaultCloud() in WedaApplicationBuilder for DI-based logger.
        /// </summary>
        public static IWedaCloudService Default
        {
            get
            {
                var natsOpts = NatsOpts.Default with { Url = "nats://localhost:4222", SerializerRegistry = NatsClientDefaultSerializerRegistry.Default };
                var client = new NatsClient(natsOpts);
                var deviceAgentClient = new DeviceAgentClient(client, logger: null);
                var telemetryClient = new TelemetryClient(client, logger: null);
                return new WedaCloudService(client, deviceAgentClient, telemetryClient, logger: null);
            }
        }

        /// <summary>
        /// Mock cloud service for testing without real cloud connection.
        /// Note: Logger will be null. Use UseDefaultCloud() in WedaApplicationBuilder for DI-based logger.
        /// </summary>
        public static IWedaCloudService Mock => new NullCloudService(logger: null);

        /// <summary>
        /// Get cloud service from dependency injection container.
        /// Recommended approach for production applications with proper DI setup.
        /// </summary>
        /// <param name="serviceProvider">The service provider from which to resolve the cloud service.</param>
        /// <returns>The registered IWedaCloudService instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if IWedaCloudService is not registered in the container.</exception>
        public static IWedaCloudService FromServiceProvider(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IWedaCloudService>();
        }
    }
}
