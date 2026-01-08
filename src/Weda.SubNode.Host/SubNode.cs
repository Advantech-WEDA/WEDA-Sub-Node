using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;

namespace Weda.SubNode.Host;

public class SubNode : IAsyncDisposable
{
    private static readonly Lazy<SubNode> _default = new(() => new SubNode(WedaApplicationContext.Default));
    private readonly IWedaApplicationContext _context;
    private readonly List<IDevice> _devices = [];
    private readonly ILogger<SubNode> _logger;
    private bool _initialized;
    public string? Id => _context.SubNodeInfo.Id;
    
    /// <summary>
    /// Gets the default SubNode instance using WedaApplication.Default
    /// </summary>
    public static SubNode Default => _default.Value;

    /// <summary>
    /// Gets the application context
    /// </summary>
    public IWedaApplicationContext Context => _context;

    public SubNode(IWedaApplicationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.GetLogger<SubNode>();
    }

    /// <summary>
    /// Register a device to be managed by SubNode.
    /// Must be called before InitializeAsync().
    /// </summary>
    public SubNode AddDevice(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_initialized) throw new InvalidOperationException("Cannot add devices after initialization");

        _devices.Add(device);
        _logger.LogDebug("Added device: {DeviceName}", device.DeviceName);
        return this;
    }

    /// <summary>
    /// Initialize SubNode and all registered devices.
    /// Flow:
    /// 1. SubNodeManager.InitializeAsync() - connect, register SubNode, subscribe events
    /// 2. For each device: InitializeAsync() - physical connection, enrich config
    /// 3. UploadDeviceCnofigurationsAsync() - aggregate all configs, upload once
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogDebug("SubNode already initialized");
            return true;
        }

        if (_devices.Count == 0)
        {
            _logger.LogWarning("No devices registered");
            return false;
        }

        _logger.LogInformation(_devices.Count == 1
            ? "Initializing SubNode with {DeviceCount} device"
            : "Initializing SubNode with {DeviceCount} devices", 
            _devices.Count);

        // 1. Initialize SubNode (connect, register, subscribe)
        _logger.LogDebug("Initializing SubNodeManager");
        if (!await _context.SubNodeManager.InitializeAsync(cancellationToken))
        {
            _logger.LogError("Failed to initialize SubNodeManager");
            return false;   
        }
        _logger.LogInformation("SubNode initialized (SubNodeId: {SubNodeId})", Id);

        // 2. Initialize all device (establish physical connection and enrich sensors)
        _logger.LogDebug("Initializing {Count} devices", _devices.Count);
        foreach (var device in _devices)
        {
            if (!await device.InitializeAsync(cancellationToken))
            {
                _logger.LogError("Failed to initialize device: {DeviceName}", device.DeviceName);
                return false;
            }
        }

        // 3. Aggregate and upload all configurations
        _logger.LogDebug("Uploading aggregated device configurations");
        var configurations = new DeviceConfigurations(_devices);

        if (!await _context.SubNodeManager.UploadDeviceConfigurationsAsync(configurations, cancellationToken))
        {
            _logger.LogError("Failed to upload device configurations");
            return false;
        }

        _initialized = true;
        _logger.LogInformation("SubNode initialization completed successfully");
        return true;
    }

    /// <summary>
    /// Start all registered devices.
    /// Must be called after InitializeAsync().
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized) {
            await InitializeAsync(cancellationToken);
        }

        _logger.LogInformation("Starting {count} devices", _devices.Count);

        foreach (var device in _devices)
        {
            _logger.LogDebug("Starting device: {DeviceName}", device.DeviceName);
            await device.StartAsync(cancellationToken);
        }

        _logger.LogInformation("All devices started");
    }

    /// <summary>
    /// Stop all registered devices.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping {Count} devices", _devices.Count);

        foreach (var device in _devices)
        {
            try
            {
                _logger.LogDebug("Stopping device: {DeviceName}", device.DeviceName);
                await device.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping device: {DeviceName}", device.DeviceName);
            }
        }

        _logger.LogInformation("All devices stopped");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        foreach (var device in _devices)
        {
            try
            {
                device.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing device: {DeviceName}", device.DeviceName);
            }
        }

        _devices.Clear();
        GC.SuppressFinalize(this);
    }
}