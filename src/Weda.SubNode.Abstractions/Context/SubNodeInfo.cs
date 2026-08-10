using System.Reflection;

using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Information about the Sub-Node instance.
/// A Sub-Node is a single dotnet program that may manage multiple internal devices.
/// From the cloud's perspective, the entire Sub-Node is treated as a single "virtual device"
/// with one globally unique DeviceId.
/// </summary>
/// <remarks>
/// <para>
/// <b>WEDA Device Management Rules:</b>
/// </para>
/// <list type="bullet">
/// <item>Each Sub-Node has a globally unique DeviceId within WEDA Node</item>
/// <item>Each internal device has a DeviceName that is unique within the Sub-Node</item>
/// <item>Sensor ResourceIds are generated using: sha1(SubNode.DeviceId + DeviceName + SensorName)</item>
/// </list>
/// </remarks>
public class SubNodeInfo
{
    /// <summary>
    /// Current wire-contract schema version the SDK reports in the <c>subNode</c>
    /// section. <b>2</b> = the camelCase contract this SDK emits. The cloud-side
    /// validator enforces strict camelCase for v2, so every cloud-bound payload
    /// this SDK produces MUST be camelCase (envelope and parameter keys alike).
    /// A payload with no <c>schemaVersion</c> defaults to v1 (the legacy
    /// PascalCase-tolerant contract) — that is how already-deployed devices that
    /// predate this field keep validating.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// The SDK version this build reports, resolved once from the assembly
    /// informational version. Exposed statically so cloud-report assembly code
    /// (which has no <see cref="SubNodeInfo"/> instance) can stamp it.
    /// </summary>
    public static string CurrentSdkVersion => ResolvedSdkVersion;

    /// <summary>
    /// SDK version resolved once from this assembly's informational/product version.
    /// Falls back to the assembly file version, then to "1.0.0".
    /// </summary>
    private static readonly string ResolvedSdkVersion = ResolveSdkVersion();

    private static string ResolveSdkVersion()
    {
        var assembly = typeof(SubNodeInfo).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip any SemVer build-metadata suffix (e.g. "1.2.0+abc1234").
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Gets or sets the Sub-Node name used for cloud registration.
    /// This name identifies the Sub-Node when registering with WEDA Node.
    /// </summary>
    /// <example>"MyFactorySubNode"</example>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the globally unique Device ID assigned by WEDA Node during registration.
    /// This ID is used for all cloud communications (telemetry, commands, config updates).
    /// </summary>
    /// <remarks>
    /// This value is null before registration and populated after successful registration.
    /// Once registered, it is cached locally in .weda/subnode.registration.json.
    /// </remarks>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Alias of Sub-Node unique identifier.
    /// </summary>
    public string? Id => DeviceId;

    /// <summary>
    /// Gets or sets the manufacturer name.
    /// </summary>
    public string Manufacturer { get; set; } = "Advantech";

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = "SubNode-SDK";

    /// <summary>
    /// Gets or sets the software version.
    /// </summary>
    public string SwVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the SubNode SDK package version.
    /// Defaults to the SDK assembly's informational/product version resolved at runtime
    /// (with any <c>+&lt;git-sha&gt;</c> build-metadata suffix stripped).
    /// </summary>
    /// <example>"1.2.0"</example>
    public string SdkVersion { get; set; } = ResolvedSdkVersion;

    /// <summary>
    /// Gets or sets the wire-contract schema version of the reported <c>subNode</c>
    /// section. Defaults to <see cref="CurrentSchemaVersion"/>.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets or sets the device type for cloud registration.
    /// Default is CustomDevice.
    /// </summary>
    public SubNodeType SubNodeType { get; set; } = SubNodeType.CustomDevice;

    /// <summary>
    /// Gets or sets whether to automatically generate DTDL content from sensor definitions.
    /// When true:
    /// - DTDL is auto-generated based on Sensor definitions (Schema, DisplayName, Description)
    /// - Sensor.Dtmi is auto-generated using short ID generator
    /// - DtdlPath is optional (ignored if specified)
    /// When false:
    /// - DTDL must be loaded from DtdlPath
    /// - Each Sensor must have Dtmi specified
    /// Default is false for backwards compatibility.
    /// </summary>
    public bool AutoGenEnabled { get; set; } = false;

    /// <summary>
    /// Gets whether the Sub-Node has been registered with the cloud.
    /// </summary>
    public bool IsRegistered => !string.IsNullOrEmpty(DeviceId);
    
    /// <summary>
    /// The metadata of Sub-Node
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
