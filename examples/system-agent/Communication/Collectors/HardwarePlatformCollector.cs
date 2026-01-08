using Advantech.Edge.IFeatures;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

using Device = Advantech.Edge.Device;

namespace SystemAgentExample.Communication.Collectors;

/// <summary>
/// Collects hardware platform-specific metrics using the underlying hardware driver.
/// Implementation uses Advantech SUSI Driver, but the abstraction layer should not expose this detail.
/// </summary>
public class HardwarePlatformCollector
{
    private readonly ILogger _logger;
    private readonly Device? _advantechEdgeDevice;

    public HardwarePlatformCollector(ILogger logger, Device? advantechEdgeDevice)
    {
        _logger = logger;
        _advantechEdgeDevice = advantechEdgeDevice;
    }

    public HardwareInfoMetrics CollectHardwareInfoMetrics()
    {
        var metrics = new HardwareInfoMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogInformation("Hardware platform device not available, skipping hardware info metrics");
            return metrics;
        }

        try
        {
            metrics.MotherboardName = _advantechEdgeDevice.PlatformInformation.MotherboardName ?? string.Empty;
            metrics.Manufacturer = _advantechEdgeDevice.PlatformInformation.Manufacturer ?? string.Empty;
            metrics.BiosRevision = _advantechEdgeDevice.PlatformInformation.BiosRevision ?? string.Empty;
            metrics.DriverVersion = _advantechEdgeDevice.PlatformInformation.DriverVersion ?? string.Empty;
            metrics.LibraryVersion = _advantechEdgeDevice.PlatformInformation.LibraryVersion ?? string.Empty;
            metrics.EcRevision = _advantechEdgeDevice.PlatformInformation.EcRevision ?? string.Empty;

            _logger.LogInformation("Hardware platform information collected: Motherboard={Motherboard}, Manufacturer={Manufacturer}, BIOS={Bios}",
                metrics.MotherboardName, metrics.Manufacturer, metrics.BiosRevision);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect hardware platform information metrics");
        }
        return metrics;
    }

    /// <summary>
    /// Collects temperature metrics from onboard sensors.
    /// Each metric type (temperature, voltage, fanspeed) is collected separately 
    /// since they can be configured independently in settings.
    /// </summary>
    public TemperatureMetrics CollectTemperatureMetrics()
    {
        var metrics = new TemperatureMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping temperature metrics");
            return metrics;
        }

        if (!_advantechEdgeDevice.OnboardSensors.IsSupported)
        {
            _logger.LogWarning("Onboard sensors not supported on this device");
            return metrics;
        }

        try
        {
            var temperatureSources = _advantechEdgeDevice.OnboardSensors.TemperatureSources;
            foreach (var source in temperatureSources)
            {
                try
                {
                    var temperature = _advantechEdgeDevice.OnboardSensors.GetTemperature(source);
                    metrics.Temperatures[source.ToString()] = temperature;
                    _logger.LogInformation($"{source} Temperature: {temperature} °C");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to collect {source} Temperature metrics");
                }
            }

            _logger.LogInformation("Temperature metrics collected: {Count} sensors", temperatureSources.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect temperature metrics");
        }

        return metrics;
    }

    /// <summary>
    /// Collects voltage metrics from onboard sensors.
    /// </summary>
    public VoltageMetrics CollectVoltageMetrics()
    {
        var metrics = new VoltageMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping voltage metrics");
            return metrics;
        }

        if (!_advantechEdgeDevice.OnboardSensors.IsSupported)
        {
            _logger.LogWarning("Onboard sensors not supported on this device");
            return metrics;
        }

        try
        {
            var voltageSources = _advantechEdgeDevice.OnboardSensors.VoltageSources;
            foreach (var source in voltageSources)
            {
                try
                {
                    var voltage = _advantechEdgeDevice.OnboardSensors.GetVoltage(source);
                    metrics.Voltages[source.ToString()] = voltage;
                    _logger.LogInformation($"{source} Voltage: {voltage} V");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to collect {source} Voltage metrics");
                }
            }

            _logger.LogInformation("Voltage metrics collected: {Count} sensors", voltageSources.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect voltage metrics");
        }

        return metrics;
    }

    /// <summary>
    /// Collects fan speed metrics from onboard sensors.
    /// </summary>
    public FanSpeedMetrics CollectFanSpeedMetrics()
    {
        var metrics = new FanSpeedMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping fan speed metrics");
            return metrics;
        }

        if (!_advantechEdgeDevice.OnboardSensors.IsSupported)
        {
            _logger.LogWarning("Onboard sensors not supported on this device");
            return metrics;
        }

        try
        {
            var fanSources = _advantechEdgeDevice.OnboardSensors.FanSources;
            foreach (var source in fanSources)
            {
                try
                {
                    var fanSpeed = _advantechEdgeDevice.OnboardSensors.GetFanSpeed(source);
                    metrics.FanSpeeds[source.ToString()] = fanSpeed;
                    _logger.LogInformation($"{source} Fan Speed: {fanSpeed} RPM");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to collect {source} Fan Speed metrics");
                }
            }

            _logger.LogInformation("Fan speed metrics collected: {Count} fans", fanSources.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect fan speed metrics");
        }

        return metrics;
    }


    public GpioMetrics CollectGpioMetrics()
    {
        var metrics = new GpioMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping GPIO metrics");
            return metrics;
        }

        try
        {
            metrics.IsSupported = _advantechEdgeDevice.Gpio.IsSupported;

            if (!metrics.IsSupported)
            {
                _logger.LogWarning("GPIO not supported on this device");
                return metrics;
            }

            var pinNames = _advantechEdgeDevice.Gpio.PinNames;
            _logger.LogInformation($"GPIO pin list - Length: {pinNames.Length}, Names: {string.Join(", ", pinNames)}");
            metrics.PinNames = pinNames;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect GPIO metrics");
        }

        return metrics;
    }


    public WatchdogMetrics CollectWatchdogMetrics()
    {
        var metrics = new WatchdogMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping watchdog metrics");
            return metrics;
        }

        try
        {
            metrics.IsSupported = _advantechEdgeDevice.Watchdog.IsSupported;

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
    }

    public ThermalProtectionMetrics CollectThermalProtectionMetrics()
    {
        var metrics = new ThermalProtectionMetrics();

        if (_advantechEdgeDevice == null)
        {
            _logger.LogDebug("Hardware platform device not available, skipping thermal protection metrics");
            return metrics;
        }

        try
        {
            _logger.LogDebug("Checking thermal protection availability");
            metrics.IsSupported = _advantechEdgeDevice.ThermalProtection.IsSupported;

            if (!metrics.IsSupported)
            {
                _logger.LogWarning("Thermal protection is NOT supported on this device");
                return metrics;
            }

            _logger.LogInformation("Thermal protection is supported on this device");

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
    }
}
