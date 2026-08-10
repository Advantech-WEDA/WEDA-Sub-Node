using System.Collections.Concurrent;
using Advantech.Edge.IFeatures;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

using Device = Advantech.Edge.Device;

namespace SystemAgentExample.Communication.Collectors;

/// <summary>
/// Collects hardware platform-specific metrics using the underlying hardware driver.
/// 
/// CRITICAL: All calls to Advantech.Edge.Device are dispatched to a single dedicated thread.
/// The native libSusiIoT.so library has thread-affinity (uses TLS or non-thread-safe global state).
/// Calling from different OS threads — even sequentially — causes SIGSEGV (exit code 139).
/// </summary>
public class HardwarePlatformCollector : IDisposable
{
    private readonly ILogger _logger;
    private readonly Device _advantechEdgeDevice;

    /// <summary>
    /// Dedicated thread that owns all native library calls.
    /// The native library must always be called from the same OS thread.
    /// </summary>
    private readonly Thread _nativeThread;
    private readonly BlockingCollection<Action> _workQueue = new();
    private volatile bool _disposed;

    public HardwarePlatformCollector(ILogger logger)
    {
        _logger = logger;

        _nativeThread = new Thread(NativeThreadLoop)
        {
            Name = "SusiIoT-Native",
            IsBackground = true
        };
        _nativeThread.Start();

        // Initialize Device on the dedicated native thread (thread-affinity requirement)
        _advantechEdgeDevice = RunOnNativeThread(() =>
        {
            _logger.LogInformation(
                "[SIGSEGV-FIX] Advantech.Edge.Device initializing on dedicated thread (ManagedThreadId={ThreadId})",
                Environment.CurrentManagedThreadId);
            var device = new Device();
            if (device.InitializationFailed)
            {
                _logger.LogWarning("Advantech.Edge.Device initialization reported failure");
                return null;
            }
            _logger.LogInformation("[SIGSEGV-FIX] Advantech.Edge.Device initialized successfully on dedicated thread");
            return device;
        }) ?? throw new InvalidOperationException("Advantech.Edge.Device initialization failed on native thread");

        _logger.LogInformation(
            "[SIGSEGV-FIX] HardwarePlatformCollector created with dedicated native thread (ManagedThreadId={ThreadId})",
            _nativeThread.ManagedThreadId);
    }

    private void NativeThreadLoop()
    {
        _logger.LogInformation(
            "[SIGSEGV-FIX] Native thread started: ManagedThreadId={ThreadId}, OSThreadId={OSThreadId}",
            Environment.CurrentManagedThreadId,
            Environment.CurrentManagedThreadId);

        foreach (var action in _workQueue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGSEGV-FIX] Exception on native thread");
            }
        }
    }

    /// <summary>
    /// Dispatches work to the dedicated native thread and waits for completion.
    /// </summary>
    private T RunOnNativeThread<T>(Func<T> func)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HardwarePlatformCollector));

        T result = default!;
        Exception? caught = null;
        using var done = new ManualResetEventSlim(false);

        _workQueue.Add(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();

        if (caught != null)
            throw caught;

        return result;
    }

    /// <summary>
    /// Reads a load/health snapshot of the Advantech HAL (versions and per-subsystem
    /// support). Only reachable once the <see cref="Device"/> has initialized successfully, so
    /// <see cref="AdvantechHalStatus.IsLoaded"/> is always <c>true</c> here; the "failed to load"
    /// case is produced by the caller when construction throws.
    /// Marshalled to the dedicated native thread (thread-affinity requirement).
    /// </summary>
    public AdvantechHalStatus GetHalStatus()
    {
        // Backend detection reads the process module map (pure file IO), so it does not need
        // the native thread — but it must run after the Device has dlopen'd its backend, which
        // is guaranteed here since the collector only exists once initialization succeeded.
        var (backend, backendLibrary) = ResolveLoadedBackend();

        return RunOnNativeThread(() =>
        {
            var libraryName = ResolveLibraryName();
            var packageVersion = ResolvePackageVersion();
            try
            {
                var info = _advantechEdgeDevice.PlatformInformation;
                return AdvantechHalStatus.Ok(
                    name: libraryName,
                    backend: backend,
                    backendLibrary: backendLibrary,
                    packageVersion: packageVersion,
                    driverVersion: info.DriverVersion ?? string.Empty,
                    libraryVersion: info.LibraryVersion ?? string.Empty,
                    onboardSensorsSupported: _advantechEdgeDevice.OnboardSensors?.IsSupported ?? false,
                    gpioSupported: _advantechEdgeDevice.Gpio?.IsSupported ?? false,
                    watchdogSupported: _advantechEdgeDevice.Watchdog?.IsSupported ?? false,
                    thermalProtectionSupported: _advantechEdgeDevice.ThermalProtection?.IsSupported ?? false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Advantech HAL loaded but reading platform status failed");
                return AdvantechHalStatus.LoadedWithError(libraryName, backend, backendLibrary, packageVersion, ex);
            }
        });
    }

    /// <summary>
    /// Name of the backing library, read from the loaded Advantech.Edge assembly (the library result).
    /// </summary>
    private static string ResolveLibraryName()
        => typeof(Device).Assembly.GetName().Name ?? AdvantechHalStatus.DefaultName;

    /// <summary>
    /// Best-effort package version read from the loaded Advantech.Edge assembly.
    /// </summary>
    private static string ResolvePackageVersion()
        => typeof(Device).Assembly.GetName().Version?.ToString() ?? "unknown";

    // Maps native backend module markers Advantech.Edge can bind to onto the two customer-facing
    // technology names: the SUSI family (libSusiIoT.so / libSUSI-4.00.so) reports as "SUSI", and
    // the EAPI library (libEAPI.so) reports as "PlatformSDK" — per Advantech.Edge's own docs, which
    // describe libEAPI as the "PlatformSDK" library. Ordered longest-match-first so "susiiot" wins
    // over "susi".
    private static readonly (string Marker, string Backend)[] BackendMarkers =
    {
        ("susiiot", AdvantechHalStatus.SusiBackend),
        ("eapi", AdvantechHalStatus.PlatformSdkBackend),
        ("susi", AdvantechHalStatus.SusiBackend),
    };

    /// <summary>
    /// Determines which native backend library Advantech.Edge actually loaded by scanning the
    /// process module map (<c>/proc/self/maps</c>) for the mapped shared object. This is
    /// authoritative for "what is loaded right now" and independent of the SDK's internal
    /// selection API (which is not publicly accessible). Returns <c>(Unknown, "")</c> off Linux
    /// or when no known backend module is mapped.
    /// </summary>
    private (string Backend, string Library) ResolveLoadedBackend()
    {
        const string mapsPath = "/proc/self/maps";
        try
        {
            if (!File.Exists(mapsPath))
                return (AdvantechHalStatus.UnknownBackend, string.Empty);

            foreach (var (marker, backend) in BackendMarkers)
            {
                foreach (var line in File.ReadLines(mapsPath))
                {
                    var slash = line.IndexOf('/');
                    if (slash < 0)
                        continue; // anonymous / [heap] / [stack] mappings have no path

                    var moduleName = Path.GetFileName(line[slash..]);
                    if (moduleName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        return (backend, moduleName);
                }
            }

            return (AdvantechHalStatus.UnknownBackend, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine Advantech HAL native backend from process module map");
            return (AdvantechHalStatus.UnknownBackend, string.Empty);
        }
    }

    public HardwareInfoMetrics CollectHardwareInfoMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new HardwareInfoMetrics();
            try
            {
                metrics.MotherboardName = _advantechEdgeDevice.PlatformInformation.MotherboardName ?? string.Empty;
                metrics.Manufacturer = _advantechEdgeDevice.PlatformInformation.Manufacturer ?? string.Empty;
                metrics.BiosRevision = _advantechEdgeDevice.PlatformInformation.BiosRevision ?? string.Empty;
                metrics.DriverVersion = _advantechEdgeDevice.PlatformInformation.DriverVersion ?? string.Empty;
                metrics.LibraryVersion = _advantechEdgeDevice.PlatformInformation.LibraryVersion ?? string.Empty;
                metrics.EcRevision = _advantechEdgeDevice.PlatformInformation.EcRevision ?? string.Empty;

                _logger.LogDebug("Hardware platform information collected: Motherboard={Motherboard}, Manufacturer={Manufacturer}, BIOS={Bios}",
                    metrics.MotherboardName, metrics.Manufacturer, metrics.BiosRevision);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect hardware platform information metrics");
            }
            return metrics;
        });
    }

    public TemperatureMetrics CollectTemperatureMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new TemperatureMetrics();
            try
            {
                if (!_advantechEdgeDevice.OnboardSensors!.IsSupported)
                {
                    _logger.LogWarning("Onboard sensors not supported on this device");
                    return metrics;
                }

                var temperatureSources = _advantechEdgeDevice.OnboardSensors.TemperatureSources;
                foreach (var source in temperatureSources)
                {
                    try
                    {
                        var temperature = _advantechEdgeDevice.OnboardSensors.GetTemperature(source);
                        metrics.Temperatures[source.ToString()] = temperature;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to collect {Source} Temperature metrics", source);
                    }
                }
                _logger.LogDebug("Temperature metrics collected: {Count} sensors", temperatureSources.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect temperature metrics");
            }
            return metrics;
        });
    }

    public VoltageMetrics CollectVoltageMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new VoltageMetrics();
            try
            {
                if (!_advantechEdgeDevice.OnboardSensors!.IsSupported)
                {
                    _logger.LogWarning("Onboard sensors not supported on this device");
                    return metrics;
                }

                var voltageSources = _advantechEdgeDevice.OnboardSensors.VoltageSources;
                foreach (var source in voltageSources)
                {
                    try
                    {
                        var voltage = _advantechEdgeDevice.OnboardSensors.GetVoltage(source);
                        metrics.Voltages[source.ToString()] = voltage;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to collect {Source} Voltage metrics", source);
                    }
                }
                _logger.LogDebug("Voltage metrics collected: {Count} sensors", voltageSources.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect voltage metrics");
            }
            return metrics;
        });
    }

    public FanSpeedMetrics CollectFanSpeedMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new FanSpeedMetrics();
            try
            {
                if (!_advantechEdgeDevice.OnboardSensors!.IsSupported)
                {
                    _logger.LogWarning("Onboard sensors not supported on this device");
                    return metrics;
                }

                var fanSources = _advantechEdgeDevice.OnboardSensors.FanSources;
                foreach (var source in fanSources)
                {
                    try
                    {
                        var fanSpeed = _advantechEdgeDevice.OnboardSensors.GetFanSpeed(source);
                        metrics.FanSpeeds[source.ToString()] = fanSpeed;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to collect {Source} Fan Speed metrics", source);
                    }
                }
                _logger.LogDebug("Fan speed metrics collected: {Count} fans", fanSources.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect fan speed metrics");
            }
            return metrics;
        });
    }

    public GpioMetrics CollectGpioMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new GpioMetrics();
            try
            {
                metrics.IsSupported = _advantechEdgeDevice.Gpio!.IsSupported;

                if (!metrics.IsSupported)
                {
                    _logger.LogWarning("GPIO not supported on this device");
                    return metrics;
                }

                var pinNames = _advantechEdgeDevice.Gpio.PinNames;
                _logger.LogDebug("GPIO pin list - Length: {Length}, Names: {Names}",
                    pinNames.Length, string.Join(", ", pinNames));
                metrics.PinNames = pinNames;

                foreach (var pinName in pinNames)
                {
                    try
                    {
                        var level = _advantechEdgeDevice.Gpio.GetLevel(pinName);
                        if (level.HasValue)
                        {
                            metrics.PinStateDetails[pinName] = (int)level.Value;
                        }
                        else
                        {
                            _logger.LogWarning("GPIO pin '{PinName}' returned null level", pinName);
                        }
                    }
                    catch (Exception exPin)
                    {
                        _logger.LogWarning(exPin, "Exception while reading GPIO pin '{PinName}' state", pinName);
                        metrics.PinStateDetails[pinName] = int.MinValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect GPIO metrics");
            }
            return metrics;
        });
    }

    public WatchdogMetrics CollectWatchdogMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new WatchdogMetrics();
            try
            {
                metrics.IsSupported = _advantechEdgeDevice.Watchdog!.IsSupported;

                if (!metrics.IsSupported)
                {
                    _logger.LogWarning("Watchdog not supported on this device");
                }

                var timerIds = _advantechEdgeDevice.Watchdog.TimerIds ?? Array.Empty<string>();
                metrics.TimerIds = timerIds;

                if (timerIds.Length == 0)
                {
                    _logger.LogWarning("No watchdog timers available on this device");
                    return metrics;
                }

                foreach (var timerId in timerIds)
                {
                    try
                    {
                        WatchdogTimerCap? cap = null;
                        WatchdogTimerConfig? config = null;

                        if (!_advantechEdgeDevice.Watchdog.TryGetCap(timerId, out cap) || cap is null)
                        {
                            _logger.LogWarning("Failed to get capabilities for watchdog timer '{TimerId}'", timerId);
                            continue;
                        }

                        if (!_advantechEdgeDevice.Watchdog.TryGetConfig(timerId, out config) || config is null)
                        {
                            _logger.LogWarning("Failed to get current config for watchdog timer '{TimerId}'", timerId);
                            continue;
                        }

                        metrics.TimerDetails[timerId] = new
                        {
                            Cap = cap,
                            Config = config
                        };

                        _logger.LogDebug(
                            "Watchdog Timer '{TimerId}' read: IsStoppable={IsStoppable}, Delay[min/max]={DelayMin}/{DelayMax}, Event[min/max]={EventMin}/{EventMax}, Reset[min/max]={ResetMin}/{ResetMax}, SupportFlags={Flags}",
                            timerId,
                            cap.IsStoppable,
                            cap.DelayMinimum, cap.DelayMaximum,
                            cap.EventMinimum, cap.EventMaximum,
                            cap.ResetMinimum, cap.ResetMaximum,
                            cap.SupportFlags
                        );
                    }
                    catch (Exception exTimer)
                    {
                        _logger.LogWarning(exTimer, "Exception while reading watchdog timer '{TimerId}' details", timerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect watchdog metrics");
            }
            return metrics;
        });
    }

    public ThermalProtectionMetrics CollectThermalProtectionMetrics()
    {
        return RunOnNativeThread(() =>
        {
            var metrics = new ThermalProtectionMetrics();
            try
            {
                metrics.IsSupported = _advantechEdgeDevice.ThermalProtection!.IsSupported;

                if (!metrics.IsSupported)
                {
                    _logger.LogWarning("Thermal protection is NOT supported on this device");
                    return metrics;
                }

                var zoneIds = _advantechEdgeDevice.ThermalProtection.ZoneIds ?? Array.Empty<string>();
                metrics.ZoneIds = zoneIds;

                if (zoneIds.Length == 0)
                {
                    _logger.LogWarning("No thermal protection zones available on this device");
                    return metrics;
                }

                foreach (var zoneId in zoneIds)
                {
                    try
                    {
                        ThermalProtectionZoneCap? cap = null;
                        ThermalProtectionZoneConfig? config = null;

                        if (!_advantechEdgeDevice.ThermalProtection.TryGetZoneCap(zoneId, out cap) || cap is null)
                        {
                            _logger.LogWarning("Failed to get capabilities for thermal protection zone '{ZoneId}'", zoneId);
                            continue;
                        }

                        if (!_advantechEdgeDevice.ThermalProtection.TryGetZoneConfig(zoneId, out config) || config is null)
                        {
                            _logger.LogWarning("Failed to get current config for thermal protection zone '{ZoneId}'", zoneId);
                            continue;
                        }

                        metrics.ZoneDetails[zoneId] = new
                        {
                            Cap = cap,
                            Config = config
                        };

                        _logger.LogDebug(
                            "Thermal Zone '{ZoneId}' read: Sources={Sources}, Flags={Flags}, SendEventTemp[min/max]={SendMin}/{SendMax}, ClearEventTemp[min/max]={ClearMin}/{ClearMax}, CurrentSource={Source}, EventType={EventType}",
                            zoneId,
                            cap.SupportSources,
                            cap.SupportFlags,
                            cap.SendEventTemperatureMinimum, cap.SendEventTemperatureMaximum,
                            cap.ClearEventTemperatureMinimum, cap.ClearEventTemperatureMaximum,
                            config.Source,
                            config.EventType
                        );
                    }
                    catch (Exception exZone)
                    {
                        _logger.LogWarning(exZone, "Exception while reading thermal protection zone '{ZoneId}' details", zoneId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect thermal protection metrics");
            }
            return metrics;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workQueue.CompleteAdding();
        _nativeThread.Join(TimeSpan.FromSeconds(5));
        _workQueue.Dispose();
    }
}
