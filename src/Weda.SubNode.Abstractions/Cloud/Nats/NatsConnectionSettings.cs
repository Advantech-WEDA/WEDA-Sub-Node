using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Net;
using Weda.SubNode.Abstractions.Utilities;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// NATS authentication strategy types
/// </summary>
public enum NatsAuthStrategy
{
    /// <summary>
    /// No authentication (anonymous connection)
    /// </summary>
    None,

    /// <summary>
    /// Plain text username and password authentication
    /// Requires: Username, Password
    /// </summary>
    UserPassword,

    /// <summary>
    /// Token-based authentication
    /// Requires: Token
    /// </summary>
    Token,

    /// <summary>
    /// TLS client certificate authentication
    /// Requires: TlsCertPath, TlsKeyPath (optional: TlsCaPath)
    /// </summary>
    TlsCert,

    /// <summary>
    /// Credential file authentication (JWT + NKey)
    /// Requires: CredFile
    /// </summary>
    CredFile
}

/// <summary>
/// Settings for a named NATS connection.
/// Can be configured programmatically or loaded from systemcfg.json.
/// </summary>
public record NatsConnectionSettings
{
    /// <summary>
    /// Configuration section name within SystemCfg.
    /// Full path when using IConfiguration: "SystemConfig:WedaNode"
    /// </summary>
    public const string SectionName = "WedaNode";

    private const string NatsSchemePrefix = "nats://";

    public static readonly NatsConnectionSettings Default = new();

    private string _url = "localhost:4222";

    /// <summary>
    /// URL for the NATS connection.
    /// The "nats://" prefix is automatically added if not present.
    /// </summary>
    public string Url
    {
        get => _url.EnsurePrefix(NatsSchemePrefix);
        set => _url = value ?? "localhost:4222";
    }

    /// <summary>
    /// Connection name for identification
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// Serializer type name for JSON configuration (e.g., "json", "protobuf", "default")
    /// </summary>
    public string SerializerType { get; set; } = "json";

    // ===== Authentication Settings =====

    /// <summary>
    /// Authentication strategy to use for NATS connection.
    /// Default is None (anonymous).
    /// </summary>
    public NatsAuthStrategy AuthStrategy { get; set; } = NatsAuthStrategy.None;

    /// <summary>
    /// Username for UserPassword authentication strategy.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for UserPassword authentication strategy.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Token for Token authentication strategy.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Path to the credential file for CredFile authentication strategy.
    /// The credential file contains JWT and NKey seed.
    /// </summary>
    public string? CredFile { get; set; }

    // ===== TLS Certificate Settings =====

    /// <summary>
    /// Path to the TLS client certificate file (PEM or PFX format).
    /// Required for TlsCert authentication strategy.
    /// </summary>
    public string? TlsCertPath { get; set; }

    /// <summary>
    /// Path to the TLS client private key file (PEM format).
    /// Required for TlsCert authentication strategy when using PEM certificate.
    /// </summary>
    public string? TlsKeyPath { get; set; }

    /// <summary>
    /// Path to the CA certificate file for server verification.
    /// Optional for TlsCert authentication strategy.
    /// </summary>
    public string? TlsCaPath { get; set; }

    /// <summary>
    /// Serializer registry for the NATS connection
    /// </summary>
    [JsonIgnore]
    public INatsSerializerRegistry NatsSerializerRegistry { get; set; } = NatsClientDefaultSerializerRegistry.Default;

    /// <summary>
    /// Build NatsAuthOpts from the current settings based on AuthStrategy.
    /// </summary>
    /// <returns>Configured NatsAuthOpts instance</returns>
    public NatsAuthOpts BuildAuthOpts()
    {
        return AuthStrategy switch
        {
            NatsAuthStrategy.None => NatsAuthOpts.Default,

            NatsAuthStrategy.UserPassword => NatsAuthOpts.Default with
            {
                Username = Username ?? throw new InvalidOperationException(
                    "Username is required for UserPassword authentication strategy"),
                Password = Password ?? throw new InvalidOperationException(
                    "Password is required for UserPassword authentication strategy")
            },

            NatsAuthStrategy.Token => NatsAuthOpts.Default with
            {
                Token = Token ?? throw new InvalidOperationException(
                    "Token is required for Token authentication strategy")
            },

            NatsAuthStrategy.CredFile => NatsAuthOpts.Default with
            {
                CredsFile = CredFile ?? throw new InvalidOperationException(
                    "CredFile is required for CredFile authentication strategy")
            },

            NatsAuthStrategy.TlsCert => NatsAuthOpts.Default,
            // Note: TLS certificate is handled separately via NatsTlsOpts, not NatsAuthOpts

            _ => throw new InvalidOperationException($"Unknown authentication strategy: {AuthStrategy}")
        };
    }
}
