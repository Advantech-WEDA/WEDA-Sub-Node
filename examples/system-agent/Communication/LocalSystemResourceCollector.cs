using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Collectors;
using SystemAgentExample.Models;
using SystemAgentExample.Protocols;

using Device = Advantech.Edge.Device;

namespace SystemAgentExample.Communication;

/// <summary>
/// Collects raw system resource metrics from operating system APIs.
/// This is the "communication layer" that interfaces with the local system.
/// Delegates to specialized collectors for each metric type.
/// Part of the Communication layer.
/// </summary>
public class LocalSystemResourceCollector
{
    private readonly ILogger _logger;

    private readonly CpuCollector _cpuCollector;
    private readonly RamCollector _ramCollector;
    private readonly DiskCollector _diskCollector;
    private readonly NetworkCollector _networkCollector;
    private readonly SystemCollector _systemCollector;
    private readonly GpuCollector _gpuCollector;
    private readonly HardwarePlatformCollector? _hardwarePlatformCollector;

    public LocalSystemResourceCollector(ILogger logger)
    {
        _logger = logger;

        _cpuCollector = new CpuCollector(logger);
        _ramCollector = new RamCollector(logger);
        _diskCollector = new DiskCollector(logger);
        _networkCollector = new NetworkCollector(logger);
        _systemCollector = new SystemCollector(logger);
        _gpuCollector = new GpuCollector(logger);

        // Attempt to initialize Advantech Device
        // If initialization fails (e.g., non-Advantech hardware), log warning and continue
        Device? advantechEdgeDevice = null;

        try
        {
            advantechEdgeDevice = new Device();
            _logger.LogInformation("Hardware platform device initialized successfully");
        }
        catch (TypeInitializationException typeEx)
        {
            _logger.LogWarning(typeEx, "Advantech Device initialization failed (static initializer). Hardware metrics unavailable.");
        }
        catch (System.Reflection.TargetInvocationException targetEx)
        {
            _logger.LogWarning(targetEx, "Advantech Device initialization failed (reflection). Hardware metrics unavailable.");
        }
        catch (DllNotFoundException dllEx)
        {
            _logger.LogWarning(dllEx, "Advantech driver DLL not found. Hardware metrics unavailable.");
        }
        catch (BadImageFormatException badImageEx)
        {
            _logger.LogWarning(badImageEx, "Advantech driver architecture mismatch. Hardware metrics unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Advantech Device initialization failed ({ExceptionType}). Hardware metrics unavailable.", ex.GetType().Name);
        }

        _hardwarePlatformCollector = new HardwarePlatformCollector(_logger, advantechEdgeDevice);
    }

    /// <summary>
    /// Collects all system metrics and returns raw data.
    /// </summary>
    public async Task<SystemMetricsRawData> CollectAllMetricsAsync(CancellationToken ct)
    {
        return await CollectMetricsAsync(
            new HashSet<string>
            {
                SupportedDataType.Cpu,
                SupportedDataType.Memory,
                SupportedDataType.Disk,
                SupportedDataType.Network,
                SupportedDataType.Gpu,
                SupportedDataType.System,
                SupportedDataType.Temperature,
                SupportedDataType.Voltage,
                SupportedDataType.Fanspeed,
                SupportedDataType.Hwinfo,
                SupportedDataType.Gpio,
                SupportedDataType.Watchdog,
                SupportedDataType.Thermalprotection
            },
            ct);
    }

    /// <summary>
    /// Collects only the requested metric categories and returns raw data.
    /// This is more efficient when only specific metrics are needed.
    /// </summary>
    /// <param name="metricTypes">Set of metric types to collect: "cpu", "memory", "disk", "network", "system", "gpu", "temperature", "voltage", "fanspeed", "hwinfo", "gpio", "watchdog", "thermalprotection"</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<SystemMetricsRawData> CollectMetricsAsync(HashSet<string> metricTypes, CancellationToken ct)
    {
        var rawData = new SystemMetricsRawData
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        var tasks = new List<Task>();

        Task<CpuMetrics>? cpuTask = null;
        Task<RamMetrics>? ramTask = null;
        Task<List<DiskMetrics>>? diskTask = null;
        Task<List<NetworkMetrics>>? networkTask = null;
        Task<SystemMetrics>? systemTask = null;
        Task<GpuMetrics>? gpuTask = null;

        Task<HardwareInfoMetrics>? hardwareInfoTask = null;
        Task<TemperatureMetrics>? temperatureTask = null;
        Task<VoltageMetrics>? voltageTask = null;
        Task<FanSpeedMetrics>? fanSpeedTask = null;
        Task<GpioMetrics>? gpioTask = null;
        Task<WatchdogMetrics>? watchdogTask = null;
        Task<ThermalProtectionMetrics>? thermalProtectionTask = null;

        foreach (var metricType in metricTypes)
        {
            switch (metricType.ToLowerInvariant())
            {
                case SupportedDataType.Cpu:
                    cpuTask = _cpuCollector.CollectCpuMetricsAsync(ct);
                    tasks.Add(cpuTask);
                    break;

                case SupportedDataType.Memory:
                    ramTask = _ramCollector.CollectRamMetricsAsync(ct);
                    tasks.Add(ramTask);
                    break;

                case SupportedDataType.Disk:
                    diskTask = _diskCollector.CollectDiskMetricsAsync(ct);
                    tasks.Add(diskTask);
                    break;

                case SupportedDataType.Network:
                    networkTask = Task.Run(() => _networkCollector.CollectNetworkMetrics(), ct);
                    tasks.Add(networkTask);
                    break;

                case SupportedDataType.System:
                    systemTask = _systemCollector.CollectSystemMetricsAsync(ct);
                    tasks.Add(systemTask);
                    break;

                case SupportedDataType.Gpu:
                    gpuTask = Task.Run(() => _gpuCollector.CollectGpuMetrics(), ct);
                    tasks.Add(gpuTask);
                    break;

                case SupportedDataType.Hwinfo:
                    if (_hardwarePlatformCollector != null)
                    {
                        hardwareInfoTask = Task.Run(() => _hardwarePlatformCollector.CollectHardwareInfoMetrics(), ct);
                        tasks.Add(hardwareInfoTask);
                    }
                    break;

                case SupportedDataType.Temperature:
                    if (_hardwarePlatformCollector != null)
                    {
                        temperatureTask = Task.Run(() => _hardwarePlatformCollector.CollectTemperatureMetrics(), ct);
                        tasks.Add(temperatureTask);
                    }
                    break;

                case SupportedDataType.Voltage:
                    if (_hardwarePlatformCollector != null)
                    {
                        voltageTask = Task.Run(() => _hardwarePlatformCollector.CollectVoltageMetrics(), ct);
                        tasks.Add(voltageTask);
                    }
                    break;

                case SupportedDataType.Fanspeed:
                    if (_hardwarePlatformCollector != null)
                    {
                        fanSpeedTask = Task.Run(() => _hardwarePlatformCollector.CollectFanSpeedMetrics(), ct);
                        tasks.Add(fanSpeedTask);
                    }
                    break;

                case SupportedDataType.Gpio:
                    if (_hardwarePlatformCollector != null)
                    {
                        gpioTask = Task.Run(() => _hardwarePlatformCollector.CollectGpioMetrics(), ct);
                        tasks.Add(gpioTask);
                    }
                    break;

                case SupportedDataType.Watchdog:
                    if (_hardwarePlatformCollector != null)
                    {
                        watchdogTask = Task.Run(() => _hardwarePlatformCollector.CollectWatchdogMetrics(), ct);
                        tasks.Add(watchdogTask);
                    }
                    break;

                case SupportedDataType.Thermalprotection:
                    if (_hardwarePlatformCollector != null)
                    {
                        thermalProtectionTask = Task.Run(() => _hardwarePlatformCollector.CollectThermalProtectionMetrics(), ct);
                        tasks.Add(thermalProtectionTask);
                    }
                    break;
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        if (cpuTask != null) rawData.Cpu = await cpuTask;
        if (ramTask != null) rawData.Ram = await ramTask;
        if (diskTask != null) rawData.Disks = await diskTask;
        if (networkTask != null) rawData.Networks = await networkTask;
        if (systemTask != null) rawData.System = await systemTask;
        if (gpuTask != null) rawData.Gpu = await gpuTask;

        if (hardwareInfoTask != null) rawData.HardwareInfo = await hardwareInfoTask;
        if (temperatureTask != null) rawData.Temperature = await temperatureTask;
        if (voltageTask != null) rawData.Voltage = await voltageTask;
        if (fanSpeedTask != null) rawData.FanSpeed = await fanSpeedTask;
        if (gpioTask != null) rawData.Gpio = await gpioTask;
        if (watchdogTask != null) rawData.Watchdog = await watchdogTask;
        if (thermalProtectionTask != null) rawData.ThermalProtection = await thermalProtectionTask;

        return rawData;
    }
}
