using Weda.SubNode.Abstractions.Cloud.Nats;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// System-level configuration loaded from systemcfg.json.
/// Contains settings that are typically not changed frequently and are system-wide.
/// Can be configured programmatically or loaded from configuration files.
/// </summary>
/// <example>
/// Programmatic configuration:
/// <code>
/// var systemCfg = new SystemCfg
/// {
///     WedaNode = new NatsConnectionSettings
///     {
///         Url = "nats://localhost:4222",
///         AuthStrategy = NatsAuthStrategy.UserPassword,
///         Username = "user",
///         Password = "pass"
///     }
/// };
/// </code>
///
/// systemcfg.json:
/// <code>
/// {
///   "WedaNode": {
///     "Url": "nats://localhost:4222",
///     "AuthStrategy": "UserPassword",
///     "Username": "user",
///     "Password": "pass",
///     "Name": "default",
///     "SerializerType": "json"
///   }
/// }
/// </code>
/// </example>
public class SystemCfg
{
    /// <summary>
    /// The configuration section name for the root system configuration.
    /// </summary>
    public const string SectionName = "SystemConfig";

    /// <summary>
    /// NATS connection settings for connecting to WEDA Core.
    /// Maps to the "WedaNode" section in systemcfg.json.
    /// </summary>
    public NatsConnectionSettings WedaNode { get; set; } = new();
}