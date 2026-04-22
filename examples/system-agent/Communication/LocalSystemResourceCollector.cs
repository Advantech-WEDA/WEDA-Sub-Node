using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Collectors;
using SystemAgentExample.Models;
using SystemAgentExample.Protocols;

using Weda.SubNode.Core.Policies;

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
            _logger.LogInformation("Advantech.Edge.Device initializing...");
            advantechEdgeDevice = new Device();
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

        if (advantechEdgeDevice == null || advantechEdgeDevice.InitializationFailed)
        {
            _logger.LogWarning("Device not available Advantech.Edge.Device, skipping new HardwarePlatformCollector");
        }
        else
        {
            _logger.LogInformation("Advantech.Edge.Device initialized successfully");
            _hardwarePlatformCollector = new HardwarePlatformCollector(_logger, advantechEdgeDevice);
            _logger.LogInformation("HardwarePlatformCollector initialized successfully");
        }
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

        Task<CpuMetrics?>? cpuTask = null;
        Task<RamMetrics?>? ramTask = null;
        Task<List<DiskMetrics>?>? diskTask = null;
        Task<List<NetworkMetrics>?>? networkTask = null;
        Task<SystemMetrics?>? systemTask = null;
        Task<GpuMetrics?>? gpuTask = null;

        Task<HardwareInfoMetrics?>? hardwareInfoTask = null;
        Task<TemperatureMetrics?>? temperatureTask = null;
        Task<VoltageMetrics?>? voltageTask = null;
        Task<FanSpeedMetrics?>? fanSpeedTask = null;
        Task<GpioMetrics?>? gpioTask = null;
        Task<WatchdogMetrics?>? watchdogTask = null;
        Task<ThermalProtectionMetrics?>? thermalProtectionTask = null;

        foreach (var metricType in metricTypes)
        {
            switch (metricType.ToLowerInvariant())
            {
                case SupportedDataType.Cpu:
                    cpuTask = ExecuteWithRetryAsync(t => _cpuCollector.CollectCpuMetricsAsync(t), "CPU", ct);
                    tasks.Add(cpuTask);
                    break;

                case SupportedDataType.Memory:
                    ramTask = ExecuteWithRetryAsync(t => _ramCollector.CollectRamMetricsAsync(t), "Memory", ct);
                    tasks.Add(ramTask);
                    break;

                case SupportedDataType.Disk:
                    diskTask = ExecuteWithRetryAsync(t => _diskCollector.CollectDiskMetricsAsync(t), "Disk", ct);
                    tasks.Add(diskTask);
                    break;

                case SupportedDataType.Network:
                    networkTask = ExecuteWithRetryAsync(t => Task.Run(() => _networkCollector.CollectNetworkMetrics(), t), "Network", ct);
                    tasks.Add(networkTask);
                    break;

                case SupportedDataType.System:
                    systemTask = ExecuteWithRetryAsync(t => _systemCollector.CollectSystemMetricsAsync(t), "System", ct);
                    tasks.Add(systemTask);
                    break;

                case SupportedDataType.Gpu:
                    gpuTask = ExecuteWithRetryAsync(t => Task.Run(() => _gpuCollector.CollectGpuMetrics(), t), "GPU", ct);
                    tasks.Add(gpuTask);
                    break;

                case SupportedDataType.Hwinfo:
                    if (_hardwarePlatformCollector != null)
                    {
                        hardwareInfoTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectHardwareInfoMetrics(), t), "HardwareInfo", ct);
                        tasks.Add(hardwareInfoTask);
                    }
                    break;

                case SupportedDataType.Temperature:
                    if (_hardwarePlatformCollector != null)
                    {
                        temperatureTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectTemperatureMetrics(), t), "Temperature", ct);
                        tasks.Add(temperatureTask);
                    }
                    break;

                case SupportedDataType.Voltage:
                    if (_hardwarePlatformCollector != null)
                    {
                        voltageTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectVoltageMetrics(), t), "Voltage", ct);
                        tasks.Add(voltageTask);
                    }
                    break;

                case SupportedDataType.Fanspeed:
                    if (_hardwarePlatformCollector != null)
                    {
                        fanSpeedTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectFanSpeedMetrics(), t), "FanSpeed", ct);
                        tasks.Add(fanSpeedTask);
                    }
                    break;

                case SupportedDataType.Gpio:
                    if (_hardwarePlatformCollector != null)
                    {
                        gpioTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectGpioMetrics(), t), "GPIO", ct);
                        tasks.Add(gpioTask);
                    }
                    break;

                case SupportedDataType.Watchdog:
                    if (_hardwarePlatformCollector != null)
                    {
                        watchdogTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectWatchdogMetrics(), t), "Watchdog", ct);
                        tasks.Add(watchdogTask);
                    }
                    break;

                case SupportedDataType.Thermalprotection:
                    if (_hardwarePlatformCollector != null)
                    {
                        thermalProtectionTask = ExecuteWithRetryAsync(t => Task.Run(() => _hardwarePlatformCollector.CollectThermalProtectionMetrics(), t), "ThermalProtection", ct);
                        tasks.Add(thermalProtectionTask);
                    }
                    break;
            }
        }

        if (tasks.Count > 0)
        {
            // Wait for all tasks to complete. 
            // Since ExecuteWithRetryAsync suppresses exceptions, WhenAll should not throw unless a task was cancelled or catastrophic error.
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for metric collection tasks");
                rawData.Health.IsHealthy = false;
                rawData.Health.ActiveErrors["CollectionLoop"] = ex.Message;
            }
        }

        // Safely assign results if tasks completed successfully and returned non-null
        // Also populate Health status based on failures

        if (cpuTask != null)
        {
            if (cpuTask.IsCompletedSuccessfully && cpuTask.Result != null) rawData.Cpu = cpuTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Cpu);
        }

        if (ramTask != null)
        {
            if (ramTask.IsCompletedSuccessfully && ramTask.Result != null) rawData.Ram = ramTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Memory);
        }

        if (diskTask != null)
        {
            if (diskTask.IsCompletedSuccessfully && diskTask.Result != null) rawData.Disks = diskTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Disk);
        }

        if (networkTask != null)
        {
            if (networkTask.IsCompletedSuccessfully && networkTask.Result != null) rawData.Networks = networkTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Network);
        }

        if (systemTask != null)
        {
            if (systemTask.IsCompletedSuccessfully && systemTask.Result != null) rawData.System = systemTask.Result;
            else AddCollectionError(rawData, SupportedDataType.System);
        }

        if (gpuTask != null)
        {
            if (gpuTask.IsCompletedSuccessfully && gpuTask.Result != null) rawData.Gpu = gpuTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Gpu);
        }

        if (hardwareInfoTask != null)
        {
            if (hardwareInfoTask.IsCompletedSuccessfully && hardwareInfoTask.Result != null) rawData.HardwareInfo = hardwareInfoTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Hwinfo);
        }

        if (temperatureTask != null)
        {
            if (temperatureTask.IsCompletedSuccessfully && temperatureTask.Result != null) rawData.Temperature = temperatureTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Temperature);
        }

        if (voltageTask != null)
        {
            if (voltageTask.IsCompletedSuccessfully && voltageTask.Result != null) rawData.Voltage = voltageTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Voltage);
        }

        if (fanSpeedTask != null)
        {
            if (fanSpeedTask.IsCompletedSuccessfully && fanSpeedTask.Result != null) rawData.FanSpeed = fanSpeedTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Fanspeed);
        }

        if (gpioTask != null)
        {
            if (gpioTask.IsCompletedSuccessfully && gpioTask.Result != null) rawData.Gpio = gpioTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Gpio);
        }

        if (watchdogTask != null)
        {
            if (watchdogTask.IsCompletedSuccessfully && watchdogTask.Result != null) rawData.Watchdog = watchdogTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Watchdog);
        }

        if (thermalProtectionTask != null)
        {
            if (thermalProtectionTask.IsCompletedSuccessfully && thermalProtectionTask.Result != null) rawData.ThermalProtection = thermalProtectionTask.Result;
            else AddCollectionError(rawData, SupportedDataType.Thermalprotection);
        }

        return rawData;
    }

    private static void AddCollectionError(SystemMetricsRawData rawData, string metricType)
    {
        rawData.Health.IsHealthy = false;
        rawData.Health.ActiveErrors[metricType] = "Collection failed or timed out";
    }

    /// <summary>
    /// Executes a task with retry logic using RetryPolicyFactory.
    /// Returns default(T) (null) if all retries fail or timeout occurs.
    /// </summary>
    private async Task<T?> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> action, string metricName, CancellationToken parentToken)
    {
        var retryPipeline = RetryPolicyFactory.CreateNTimeRetry(
            _logger,
            maxRetries: 2, // Total 3 attempts
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(1));

        var attemptTimeout = TimeSpan.FromSeconds(5);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            cts.CancelAfter(attemptTimeout);
            return await retryPipeline.ExecuteAsync(async ct => await action(ct), cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (parentToken.IsCancellationRequested)
            {
                return default;
            }

            _logger.LogError("Timeout waiting for {MetricName} after {Timeout} seconds.", metricName, attemptTimeout.TotalSeconds);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect {MetricName} after all retry attempts.", metricName);
            return default;
        }
    }
}
