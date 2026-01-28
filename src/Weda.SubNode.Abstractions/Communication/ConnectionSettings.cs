namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Common connection settings for all communication types
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Maximum number of retry attempts when connection fails, -1 for always retry.
    /// </summary>
    public int MaxRetries { get; set; } = -1;

    /// <summary>
    /// Initial delay in milliseconds before first retry
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Write timeout in milliseconds
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Security settings for MQTT/TLS connections
    /// </summary>
    public SecuritySettings? Security { get; set; }

    /// <summary>
    /// Whether to lock requests (only one request at a time).
    /// Should be true for single-channel protocols like Serial Port, Modbus RTU.
    /// Default: true
    /// </summary>
    public bool RequestLock { get; set; } = true;
}

/// <summary>
/// Security settings for secure connections (TLS/SSL, authentication)
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Enable TLS/SSL encryption
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// TLS protocol version (default: Tls12 | Tls13)
    /// </summary>
    public TlsVersion TlsVersion { get; set; } = TlsVersion.Tls12 | TlsVersion.Tls13;

    /// <summary>
    /// Allow untrusted certificates (only for development/testing)
    /// WARNING: Do not use in production\!
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }

    /// <summary>
    /// Skip certificate hostname validation (only for development/testing)
    /// WARNING: Do not use in production\!
    /// </summary>
    public bool IgnoreCertificateHostnameValidation { get; set; }

    /// <summary>
    /// Path to CA certificate file for server verification (PEM format)
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Path to client certificate file for mTLS (PFX/P12 format)
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Password for client certificate (if encrypted)
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Username for MQTT authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for MQTT authentication
    /// </summary>
    public string? Password { get; set; }
}

/// <summary>
/// TLS protocol versions (can be combined with bitwise OR)
/// </summary>
[Flags]
public enum TlsVersion
{
    None = 0,
    Tls10 = 1 << 0,
    Tls11 = 1 << 1,
    Tls12 = 1 << 2,
    Tls13 = 1 << 3
}
