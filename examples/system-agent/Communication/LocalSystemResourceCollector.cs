using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Collectors;
using SystemAgentExample.Models;
using SystemAgentExample.Protocols;

using Weda.SubNode.Core.Policies;

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
    private readonly AdvantechHalStatus _halStatus;

    /// <summary>
    /// Load/health snapshot of the Advantech HAL captured at construction time.
    /// Reported in the SubNode capability <c>deviceInfo.advantechHal</c>. When the HAL fails to load,
    /// this carries <see cref="AdvantechHalStatus.IsLoaded"/> = <c>false</c> and the failure reason.
    /// </summary>
    public AdvantechHalStatus HalStatus => _halStatus;

    public LocalSystemResourceCollector(ILogger logger)
    {
        _logger = logger;

        _cpuCollector = new CpuCollector(logger);
        _ramCollector = new RamCollector(logger);
        _diskCollector = new DiskCollector(logger);
        _networkCollector = new NetworkCollector(logger);
        _systemCollector = new SystemCollector(logger);
        _gpuCollector = new GpuCollector(logger);

        // Attempt to initialize Advantech Device via HardwarePlatformCollector.
        // CRITICAL: Device initialization AND all subsequent native calls MUST happen on the
        // same OS thread due to native library thread-affinity (TLS). 
        // HardwarePlatformCollector handles this via its dedicated native thread.
        try
        {
            _logger.LogInformation("Initializing HardwarePlatformCollector (Device init on dedicated native thread)...");
            _hardwarePlatformCollector = new HardwarePlatformCollector(_logger);
            _halStatus = _hardwarePlatformCollector.GetHalStatus();
            _logger.LogInformation(
                "HardwarePlatformCollector initialized successfully (Advantech HAL loaded: backend={Backend} ({BackendLibrary}), package={Package}, driver={Driver}, library={Library})",
                _halStatus.Backend, _halStatus.BackendLibrary, _halStatus.PackageVersion, _halStatus.DriverVersion, _halStatus.LibraryVersion);
        }
        catch (Exception ex)
        {
            _halStatus = AdvantechHalStatus.Failed(ex);
            _logger.LogWarning(ex, "HardwarePlatformCollector initialization failed ({ExceptionType}). Hardware metrics unavailable.", ex.GetType().Name);
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

        // Collect which hardware metric types are requested
        var hardwareMetricTypes = new HashSet<string>();

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
                        hardwareMetricTypes.Add(SupportedDataType.Hwinfo);
                    break;

                case SupportedDataType.Temperature:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Temperature);
                    break;

                case SupportedDataType.Voltage:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Voltage);
                    break;

                case SupportedDataType.Fanspeed:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Fanspeed);
                    break;

                case SupportedDataType.Gpio:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Gpio);
                    break;

                case SupportedDataType.Watchdog:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Watchdog);
                    break;

                case SupportedDataType.Thermalprotection:
                    if (_hardwarePlatformCollector != null)
                        hardwareMetricTypes.Add(SupportedDataType.Thermalprotection);
                    break;
            }
        }

        // Hardware platform metrics are collected SEQUENTIALLY in a SINGLE task
        // to prevent concurrent P/Invoke calls to the non-thread-safe libSusiIoT.so native library.
        // Concurrent calls cause SIGSEGV (exit code 139).
        Task? hardwareTask = null;
        if (_hardwarePlatformCollector != null && hardwareMetricTypes.Count > 0)
        {
            hardwareTask = Task.Run(() =>
            {
                var threadId = Environment.CurrentManagedThreadId;
                _logger.LogDebug(
                    "[SIGSEGV-FIX] Hardware metrics sequential task started on Thread {ThreadId}. Types: {Types}",
                    threadId, string.Join(", ", hardwareMetricTypes));
                var sw = System.Diagnostics.Stopwatch.StartNew();

                if (hardwareMetricTypes.Contains(SupportedDataType.Hwinfo))
                    rawData.HardwareInfo = _hardwarePlatformCollector.CollectHardwareInfoMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Temperature))
                    rawData.Temperature = _hardwarePlatformCollector.CollectTemperatureMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Voltage))
                    rawData.Voltage = _hardwarePlatformCollector.CollectVoltageMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Fanspeed))
                    rawData.FanSpeed = _hardwarePlatformCollector.CollectFanSpeedMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Gpio))
                    rawData.Gpio = _hardwarePlatformCollector.CollectGpioMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Watchdog))
                    rawData.Watchdog = _hardwarePlatformCollector.CollectWatchdogMetrics();
                if (hardwareMetricTypes.Contains(SupportedDataType.Thermalprotection))
                    rawData.ThermalProtection = _hardwarePlatformCollector.CollectThermalProtectionMetrics();

                sw.Stop();
                _logger.LogDebug(
                    "[SIGSEGV-FIX] Hardware metrics sequential task completed on Thread {ThreadId} in {ElapsedMs}ms",
                    threadId, sw.ElapsedMilliseconds);
            }, ct);
            tasks.Add(hardwareTask);
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

        if (hardwareTask != null && hardwareTask.IsFaulted)
        {
            _logger.LogError(hardwareTask.Exception, "Hardware platform metric collection failed");
            foreach (var metricType in hardwareMetricTypes)
                AddCollectionError(rawData, metricType);
        }

        return rawData;
    }

    public List<GpioPinDescriptor> ListGpioPins() => _hardwarePlatformCollector?.ListGpioPins() ?? [];

    public bool? GetGpioPinLevel(string pinName) => _hardwarePlatformCollector?.GetGpioPinLevel(pinName);

    public bool SetGpioPinLevel(string pinName, bool state) => _hardwarePlatformCollector?.SetGpioPinLevel(pinName, state) ?? false;

    public string? GetGpioPinDirection(string pinName) => _hardwarePlatformCollector?.GetGpioPinDirection(pinName);

    /// <summary>
    /// Discovers available resource names for sensor resolution (Auto-detect mode).
    /// Returns network interface names, GPIO pin names, and temperature source names.
    /// </summary>
    public DiscoveredResources DiscoverAvailableResources()
    {
        var networkInterfaces = _networkCollector.CollectNetworkMetrics()
            .Select(n => n.InterfaceName)
            .ToList();

        // For GPIO and temperature sources, we need to handle potential exceptions since they may not be supported on all hardware
        List<string> gpioPins = [];
        List<string> temperatureSources = [];

        if (_hardwarePlatformCollector != null)
        {
            try
            {
                var gpio = _hardwarePlatformCollector.CollectGpioMetrics();
                if (gpio.IsSupported)
                    gpioPins = gpio.PinNames?.ToList() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover GPIO pins");
            }

            try
            {
                var temp = _hardwarePlatformCollector.CollectTemperatureMetrics();
                temperatureSources = temp.Temperatures.Keys.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover temperature sources");
            }
        }

        return new DiscoveredResources(networkInterfaces, gpioPins, temperatureSources);
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
            return await retryPipeline.ExecuteAsync(async ct =>
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(attemptTimeout);
                return await action(attemptCts.Token);
            }, parentToken);
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
