using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core.Cloud;

namespace Weda.SubNode.Cloud;

/// <summary>
/// Static factory for creating WedaNode instances
/// Provides convenient creation methods following the pattern:
/// - Cloud.Default() - Anonymous connection (NatsAuthStrategy.None)
/// - Cloud.Default(settings) - With NatsConnectionSettings
/// - Cloud.Default(url, username, password) - Username/Password auth
/// - Cloud.Default(url, token) - Token auth
/// - Cloud.Default(url, credFile) - Credential file auth (JWT + NKey)
/// </summary>
public static class Cloud
{
    public static IWedaCloudService Mock() => new MockCloudService();

    #region Default Overloads (5 authentication strategies)

    /// <summary>
    /// Create a WedaCloudService instance with anonymous connection (NatsAuthStrategy.None).
    /// </summary>
    /// <param name="url">The url of NATS server (default: nats://localhost:4222)</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(
        string url = "nats://localhost:4222",
        ILogger<WedaCloudService>? logger = null)
    {
        var settings = new NatsConnectionSettings { Url = url, AuthStrategy = NatsAuthStrategy.None };
        return Default(settings, logger);
    }

    /// <summary>
    /// Create a WedaCloudService instance with NatsConnectionSettings.
    /// Supports all authentication strategies configured in settings.
    /// </summary>
    /// <param name="settings">NATS connection settings including auth configuration</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(
        NatsConnectionSettings settings,
        ILogger<WedaCloudService>? logger = null)
    {
        var natsOpts = NatsOpts.Default with
        {
            Url = settings.Url,
            AuthOpts = settings.BuildAuthOpts(),
            SerializerRegistry = WedaNatsSerializerRegistry.Default
        };
        return CreateFromOpts(natsOpts, logger);
    }

    /// <summary>
    /// Create a WedaCloudService instance with username/password authentication (NatsAuthStrategy.UserPassword).
    /// </summary>
    /// <param name="url">The url of NATS server</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Password for authentication</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(
        string url,
        string username,
        string password,
        ILogger<WedaCloudService>? logger = null)
    {
        var settings = new NatsConnectionSettings
        {
            Url = url,
            AuthStrategy = NatsAuthStrategy.UserPassword,
            Username = username,
            Password = password
        };
        return Default(settings, logger);
    }

    /// <summary>
    /// Create a WedaCloudService instance with token authentication (NatsAuthStrategy.Token).
    /// </summary>
    /// <param name="url">The url of NATS server</param>
    /// <param name="token">Authentication token</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(
        string url,
        NatsAuthToken token,
        ILogger<WedaCloudService>? logger = null)
    {
        var settings = new NatsConnectionSettings
        {
            Url = url,
            AuthStrategy = NatsAuthStrategy.Token,
            Token = token.Value
        };
        return Default(settings, logger);
    }

    /// <summary>
    /// Create a WedaCloudService instance with credential file authentication (NatsAuthStrategy.CredFile).
    /// </summary>
    /// <param name="url">The url of NATS server</param>
    /// <param name="credFile">Path to the credential file (JWT + NKey)</param>
    /// <param name="logger">Optional logger for cloud service</param>
    /// <returns>A new WedaCloudService instance</returns>
    public static IWedaCloudService Default(
        string url,
        NatsCredFile credFile,
        ILogger<WedaCloudService>? logger = null)
    {
        var settings = new NatsConnectionSettings
        {
            Url = url,
            AuthStrategy = NatsAuthStrategy.CredFile,
            CredFile = credFile.Path
        };
        return Default(settings, logger);
    }

    #endregion

    /// <summary>
    /// Create a WedaCloudService instance with custom clients.
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

    private static IWedaCloudService CreateFromOpts(NatsOpts natsOpts, ILogger<WedaCloudService>? logger)
    {
        var client = new NatsClient(natsOpts);
        var deviceAgentClient = new DeviceAgentClient(client, logger: null);
        var telemetryClient = new TelemetryClient(client, logger: null);

        return new WedaCloudService(client, deviceAgentClient, telemetryClient, logger: logger);
    }
}

/// <summary>
/// Wrapper type for NATS authentication token to distinguish from other string parameters.
/// </summary>
public readonly record struct NatsAuthToken(string Value)
{
    public static implicit operator NatsAuthToken(string value) => new(value);
}

/// <summary>
/// Wrapper type for NATS credential file path to distinguish from other string parameters.
/// </summary>
public readonly record struct NatsCredFile(string Path)
{
    public static implicit operator NatsCredFile(string path) => new(path);
}
