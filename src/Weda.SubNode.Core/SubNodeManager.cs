using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core;

/// <summary>
/// Manages SubNode-level operations that should only happen once for all devices.
/// Implements cloud connection, SubNode registration, event subscription, and hybrid event routing.
/// </summary>
public sealed class SubNodeManager : ISubNodeManager, IAsyncDisposable
{
    private readonly IWedaCloudService _cloudService;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly ILogger<SubNodeManager> _logger;

    private readonly ConcurrentDictionary<string, DeviceHandlers> _deviceHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IDisposable? _configSubscription;
    private IDisposable? _commandSubscription;
    private bool _isInitialized;
    private string? _subNodeId;

    public SubNodeManager(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        ILogger<SubNodeManager> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public string? SubNodeId => _subNodeId;

    /// <inheritdoc />
    public event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;

    /// <inheritdoc />
    public event Func<ExecuteCommandEvent, Task>? CommandReceived;

    /// <inheritdoc />
    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
        {
            _logger.LogDebug("SubNodeManager already initialized");
            return true;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return true;

            _logger.LogInformation("Initializing SubNodeManager for SubNode: {SubNodeName}", _subNodeInfo.Name);

            // Step 1: Connect to cloud service
            var connected = await _cloudService.ConnectAsync(ct);
            if (!connected)
            {
                _logger.LogError("Failed to connect to WedaNode");
                return false;
            }

            // Step 2: Register SubNode with cloud
            _subNodeId = await EnsureSubNodeRegisteredAsync(ct);
            if (string.IsNullOrEmpty(_subNodeId))
            {
                _logger.LogError("Failed to register SubNode with cloud");
                return false;
            }
            _logger.LogInformation("SubNode registered with ID: {SubNodeId}", _subNodeId);

            // Step 3: Subscribe to cloud events (once for all devices)
            await SubscribeToCloudEventsAsync(_subNodeId, ct);
            _logger.LogInformation("Subscribed to cloud events");

            _isInitialized = true;
            _logger.LogInformation("SubNodeManager initialization completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SubNodeManager");
            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public void RegisterDeviceHandler(
        string deviceName,
        Func<UpdateConfigurationEvent, Task> configHandler,
        Func<ExecuteCommandEvent, Task>? commandHandler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(configHandler);

        var handlers = new DeviceHandlers(configHandler, commandHandler);
        _deviceHandlers.AddOrUpdate(deviceName, handlers, (_, _) => handlers);

        _logger.LogDebug("Registered device handler for: {DeviceName}", deviceName);
    }

    /// <inheritdoc />
    public void UnregisterDeviceHandler(string deviceName)
    {
        if (_deviceHandlers.TryRemove(deviceName, out _))
        {
            _logger.LogDebug("Unregistered device handler for: {DeviceName}", deviceName);
        }
    }

    /// <summary>
    /// Ensures SubNode is registered with cloud service.
    /// </summary>
    private async Task<string?> EnsureSubNodeRegisteredAsync(CancellationToken ct)
    {
        _logger.LogDebug("Ensuring SubNode '{SubNodeName}' is registered", _subNodeInfo.Name);

        var subNodeDeviceInfo = new DeviceInfo
        {
            DeviceName = _subNodeInfo.Name,
            SubNodeType = _subNodeInfo.SubNodeType,
            Manufacturer = _subNodeInfo.Manufacturer,
            Model = $"{_subNodeInfo.Model} v{_subNodeInfo.SwVersion}",
            DeviceId = _subNodeInfo.DeviceId
        };

        return await _cloudService.GetOrRegisterDeviceIdAsync(subNodeDeviceInfo, ct);
    }

    /// <summary>
    /// Subscribes to cloud events (configuration updates and commands).
    /// </summary>
    private async Task SubscribeToCloudEventsAsync(string subNodeId, CancellationToken ct)
    {
        _logger.LogDebug("Subscribing to cloud events for SubNode: {SubNodeId}", subNodeId);

        // Subscribe to configuration updates
        _configSubscription = await _cloudService.SubscribeConfigurationUpdatesAsync(
            subNodeId,
            RouteConfigurationUpdateAsync,
            ct);

        // Subscribe to commands
        _commandSubscription = await _cloudService.SubscribeCommandsAsync(
            subNodeId,
            RouteCommandAsync,
            ct);
    }

    /// <summary>
    /// Routes configuration update events using Hybrid mode:
    /// 1. Try targeted routing based on DeviceName in message
    /// 2. Fallback to broadcast if no targeted handler found
    /// 3. Fire general event for external subscribers
    /// </summary>
    private async Task RouteConfigurationUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogDebug("Routing configuration update: ConfigType={ConfigType}, SeqId={SeqId}",
            e.ConfigType.Value, e.Message?.SeqId);

        var handled = false;

        // Step 1: Try targeted routing based on DeviceConfigs in message
        var targetDeviceNames = e.Message?.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?.Keys;

        if (targetDeviceNames != null)
        {
            foreach (var deviceName in targetDeviceNames)
            {
                if (_deviceHandlers.TryGetValue(deviceName, out var handlers))
                {
                    _logger.LogDebug("Targeted routing to device: {DeviceName}", deviceName);
                    try
                    {
                        await handlers.ConfigHandler(e);
                        handled = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in config handler for device: {DeviceName}", deviceName);
                    }
                }
            }
        }

        // Step 2: Fallback to broadcast if no targeted handlers found
        if (!handled)
        {
            _logger.LogDebug("Broadcasting configuration update to all {Count} registered devices",
                _deviceHandlers.Count);

            foreach (var (deviceName, handlers) in _deviceHandlers)
            {
                try
                {
                    await handlers.ConfigHandler(e);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in broadcast config handler for device: {DeviceName}", deviceName);
                }
            }
        }

        // Step 3: Fire general event for external subscribers
        if (ConfigurationUpdateReceived != null)
        {
            try
            {
                await ConfigurationUpdateReceived(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general ConfigurationUpdateReceived handler");
            }
        }
    }

    /// <summary>
    /// Routes command events to appropriate device handlers.
    /// </summary>
    private async Task RouteCommandAsync(ExecuteCommandEvent e)
    {
        _logger.LogDebug("Routing command: {Command}", e.Command?.DeviceCmd);

        // Commands are typically broadcast to all devices that can handle them
        // The device determines if it should handle based on command type
        foreach (var (deviceName, handlers) in _deviceHandlers)
        {
            if (handlers.CommandHandler != null)
            {
                try
                {
                    await handlers.CommandHandler(e);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in command handler for device: {DeviceName}", deviceName);
                }
            }
        }

        // Fire general event
        if (CommandReceived != null)
        {
            try
            {
                await CommandReceived(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general CommandReceived handler");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing SubNodeManager");

        _configSubscription?.Dispose();
        _commandSubscription?.Dispose();

        await _cloudService.DisconnectAsync();

        _initLock.Dispose();
        _deviceHandlers.Clear();

        _logger.LogInformation("SubNodeManager disposed");
    }

    /// <summary>
    /// Container for device-specific event handlers.
    /// </summary>
    private sealed record DeviceHandlers(
        Func<UpdateConfigurationEvent, Task> ConfigHandler,
        Func<ExecuteCommandEvent, Task>? CommandHandler);
}
