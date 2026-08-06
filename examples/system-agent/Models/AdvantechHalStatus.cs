namespace SystemAgentExample.Models;

/// <summary>
/// Load/health snapshot of the Advantech HAL (Advantech.Edge hardware-access layer) used by
/// <see cref="Communication.Collectors.HardwarePlatformCollector"/>.
///
/// This is surfaced in the SubNode capability report (<c>deviceInfo.advantechHal</c>) so the cloud
/// can see, per device, whether the native hardware-access layer loaded, which native technology
/// it bound to (<c>SUSI</c> or <c>PlatformSDK</c>), which driver/library versions are present, and
/// which hardware subsystems the platform exposes. It is intentionally immutable — construct a
/// new instance rather than mutating one.
/// </summary>
/// <param name="Name">
/// Name of the managed library, taken from the loaded library result (the Advantech.Edge
/// assembly name). Reported at <c>deviceInfo.advantechHal.name</c>. Falls back to
/// <see cref="DefaultName"/> when the library never loaded.
/// </param>
/// <param name="Backend">
/// The native technology the HAL actually bound to — <see cref="SusiBackend"/> (SUSI /
/// SusiIoT libraries), <see cref="PlatformSdkBackend"/> (EAPI library), or
/// <see cref="UnknownBackend"/>. Advantech.Edge can sit on either depending on what the device
/// exposes; this reports which one is live in this process.
/// </param>
/// <param name="BackendLibrary">
/// The concrete native module the backend loaded (e.g. <c>libSusiIoT.so</c> or <c>libEAPI.so</c>),
/// as observed in the process module map. Empty when the backend could not be determined.
/// </param>
public sealed record AdvantechHalStatus(
    string Name,
    string Backend,
    string BackendLibrary,
    bool IsLoaded,
    string PackageVersion,
    string DriverVersion,
    string LibraryVersion,
    bool OnboardSensorsSupported,
    bool GpioSupported,
    bool WatchdogSupported,
    bool ThermalProtectionSupported,
    string? FailureReason)
{
    /// <summary>Fixed <c>deviceInfo</c> key this status is published under (wire contract).</summary>
    public const string MetadataKey = "advantechHal";

    /// <summary>Fallback library name used when the library result is unavailable (load failed).</summary>
    public const string DefaultName = "Advantech.Edge";

    /// <summary>Backend label for the SUSI technology (libSusiIoT.so / libSUSI-4.00.so).</summary>
    public const string SusiBackend = "SUSI";

    /// <summary>Backend label for the PlatformSDK technology (libEAPI.so), per Advantech.Edge docs.</summary>
    public const string PlatformSdkBackend = "PlatformSDK";

    /// <summary>Reported backend when the loaded native library cannot be determined.</summary>
    public const string UnknownBackend = "Unknown";

    /// <summary>Convenience alias so callers can read <c>status.Loaded</c> in logs.</summary>
    public bool Loaded => IsLoaded;

    /// <summary>
    /// The HAL loaded successfully and its platform information was read.
    /// </summary>
    public static AdvantechHalStatus Ok(
        string name,
        string backend,
        string backendLibrary,
        string packageVersion,
        string driverVersion,
        string libraryVersion,
        bool onboardSensorsSupported,
        bool gpioSupported,
        bool watchdogSupported,
        bool thermalProtectionSupported)
        => new(
            Name: name,
            Backend: backend,
            BackendLibrary: backendLibrary,
            IsLoaded: true,
            PackageVersion: packageVersion,
            DriverVersion: driverVersion,
            LibraryVersion: libraryVersion,
            OnboardSensorsSupported: onboardSensorsSupported,
            GpioSupported: gpioSupported,
            WatchdogSupported: watchdogSupported,
            ThermalProtectionSupported: thermalProtectionSupported,
            FailureReason: null);

    /// <summary>
    /// The HAL loaded but reading platform information / feature support failed.
    /// Versions and feature flags are best-effort (may be empty/false).
    /// </summary>
    public static AdvantechHalStatus LoadedWithError(
        string name,
        string backend,
        string backendLibrary,
        string packageVersion,
        Exception ex)
        => new(
            Name: name,
            Backend: backend,
            BackendLibrary: backendLibrary,
            IsLoaded: true,
            PackageVersion: packageVersion,
            DriverVersion: string.Empty,
            LibraryVersion: string.Empty,
            OnboardSensorsSupported: false,
            GpioSupported: false,
            WatchdogSupported: false,
            ThermalProtectionSupported: false,
            FailureReason: Describe(ex));

    /// <summary>
    /// The HAL failed to load/initialize (native library missing, driver not bound,
    /// <c>Device.InitializationFailed</c>, thread-affinity crash, etc.). Hardware metrics
    /// are unavailable; only this status is reported. The library result is unavailable, so
    /// <see cref="Name"/> falls back to <see cref="DefaultName"/> and the backend is unknown.
    /// </summary>
    public static AdvantechHalStatus Failed(Exception ex)
        => new(
            Name: DefaultName,
            Backend: UnknownBackend,
            BackendLibrary: string.Empty,
            IsLoaded: false,
            PackageVersion: string.Empty,
            DriverVersion: string.Empty,
            LibraryVersion: string.Empty,
            OnboardSensorsSupported: false,
            GpioSupported: false,
            WatchdogSupported: false,
            ThermalProtectionSupported: false,
            FailureReason: Describe(ex));

    /// <summary>
    /// Projects this status into the loosely-typed <c>deviceInfo</c> metadata bag carried by
    /// the capability upload (<c>DeviceCapDto.DeviceInfo</c>). Keys use camelCase to match the
    /// wire contract; the <c>failureReason</c> key is only present when a failure occurred.
    /// A fresh dictionary is returned on every call (no shared mutable state).
    /// </summary>
    public Dictionary<string, object> ToMetadata()
    {
        var metadata = new Dictionary<string, object>
        {
            ["name"] = Name,
            ["backend"] = Backend,
            ["backendLibrary"] = BackendLibrary,
            ["loaded"] = IsLoaded,
            ["packageVersion"] = PackageVersion,
            ["driverVersion"] = DriverVersion,
            ["libraryVersion"] = LibraryVersion,
            ["features"] = new Dictionary<string, object>
            {
                ["onboardSensors"] = OnboardSensorsSupported,
                ["gpio"] = GpioSupported,
                ["watchdog"] = WatchdogSupported,
                ["thermalProtection"] = ThermalProtectionSupported,
            },
        };

        if (!string.IsNullOrEmpty(FailureReason))
        {
            metadata["failureReason"] = FailureReason;
        }

        return metadata;
    }

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
}
