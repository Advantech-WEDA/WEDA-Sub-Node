using Weda.SubNode.Abstractions.Cloud.Nats;

namespace NatsWebDashboard.Configuration;

/// <summary>
/// Strongly-typed configuration for the NATS Web Dashboard.
/// Bound from the "Dashboard" section of appsettings.json.
/// </summary>
public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// NATS connection settings. Reuses the SDK's NatsConnectionSettings
    /// which supports all auth strategies (UserPassword, Token, CredFile, TlsCert).
    /// </summary>
    public NatsConnectionSettings Nats { get; set; } = new();

    /// <summary>NATS subject pattern to subscribe (e.g., "eco1j.weda.{deviceId}.>").</summary>
    public string Subject { get; set; } = ">";

    /// <summary>HTTP server port for the dashboard.</summary>
    public int Port { get; set; } = 5050;
}
