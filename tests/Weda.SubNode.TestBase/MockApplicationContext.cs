using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Context;

namespace Weda.SubNode.TestBase;

/// <summary>
/// Mock implementation of IWedaApplicationContext for testing.
/// Provides pre-configured mock services that can be easily customized.
/// </summary>
/// <example>
/// <code>
/// var context = new MockApplicationContext();
///
/// // Setup mock behavior
/// context.MockCloudService.ConnectAsync(Arg.Any&lt;CancellationToken&gt;()).Returns(true);
/// context.MockCommunication.State.Returns(CommunicationState.Connected);
///
/// // Create device
/// var device = new ModbusDevice(context, config);
/// </code>
/// </example>
public class MockApplicationContext : IWedaApplicationContext
{
    private bool _disposed;

    /// <summary>
    /// Gets the mock cloud service.
    /// Use this to setup mock behaviors for cloud operations.
    /// </summary>
    public IWedaCloudService MockCloudService { get; }

    /// <summary>
    /// Gets the mock communication.
    /// Use this to setup mock behaviors for device communication.
    /// For Modbus devices, this is IRequestResponseCommunication&lt;byte[], byte[]&gt;.
    /// </summary>
    public IRequestResponseCommunication<byte[], byte[]> MockCommunication { get; }

    /// <summary>
    /// Gets the mock logger factory.
    /// </summary>
    public ILoggerFactory MockLoggerFactory { get; }

    /// <summary>
    /// Gets the connection options.
    /// </summary>
    public ConnectionOptions ConnectionOptions { get; set; }

    /// <summary>
    /// Gets the device options.
    /// </summary>
    public DeviceOptions DeviceOptions { get; set; }

    /// <summary>
    /// Gets the device registry.
    /// </summary>
    public IDeviceRegistry DeviceRegistry { get; }

    /// <summary>
    /// Gets the mock configuration cache.
    /// Use this to setup mock behaviors for configuration caching.
    /// </summary>
    public IConfigurationCache MockConfigurationCache { get; }

    /// <summary>
    /// Gets or sets the SubNode information for testing.
    /// </summary>
    public SubNodeInfo SubNodeInfo { get; set; }

    /// <summary>
    /// Gets or sets the device configurations for testing.
    /// </summary>
    public Dictionary<string, DeviceConfiguration> DeviceConfigsInternal { get; set; }

    /// <summary>
    /// Initializes a new instance of MockApplicationContext with default mocks.
    /// </summary>
    public MockApplicationContext()
    {
        MockCloudService = Substitute.For<IWedaCloudService>();
        MockCommunication = Substitute.For<IRequestResponseCommunication<byte[], byte[]>>();
        MockConfigurationCache = Substitute.For<IConfigurationCache>();
        MockSubNodeManager = Substitute.For<ISubNodeManager>();
        MockLoggerFactory = NullLoggerFactory.Instance;
        ConnectionOptions = ConnectionOptions.Default;
        DeviceOptions = DeviceOptions.Default;
        DeviceRegistry = new DeviceRegistry();
        DeviceConfigsInternal = new Dictionary<string, DeviceConfiguration>(StringComparer.OrdinalIgnoreCase);
        SubNodeInfo = new SubNodeInfo
        {
            Name = "TestSubNode",
            DeviceId = "test-device-id-12345",
            Manufacturer = "Test",
            Model = "MockSubNode",
            SwVersion = "1.0.0"
        };

        // Setup default behaviors
        SetupDefaultBehaviors();
    }

    /// <summary>
    /// Initializes a new instance of MockApplicationContext with custom mocks.
    /// </summary>
    /// <param name="cloudService">Custom mock cloud service.</param>
    /// <param name="communication">Custom mock communication.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="configurationCache">Optional configuration cache.</param>
    public MockApplicationContext(
        IWedaCloudService cloudService,
        IRequestResponseCommunication<byte[], byte[]> communication,
        ILoggerFactory? loggerFactory = null,
        IConfigurationCache? configurationCache = null)
    {
        MockCloudService = cloudService;
        MockCommunication = communication;
        MockConfigurationCache = configurationCache ?? Substitute.For<IConfigurationCache>();
        MockSubNodeManager = Substitute.For<ISubNodeManager>();
        MockLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        ConnectionOptions = ConnectionOptions.Default;
        DeviceOptions = DeviceOptions.Default;
        DeviceRegistry = new DeviceRegistry();
        DeviceConfigsInternal = new Dictionary<string, DeviceConfiguration>(StringComparer.OrdinalIgnoreCase);
        SubNodeInfo = new SubNodeInfo
        {
            Name = "TestSubNode",
            DeviceId = "test-device-id-12345",
            Manufacturer = "Test",
            Model = "MockSubNode",
            SwVersion = "1.0.0"
        };
    }

    #region IWedaApplicationContext Implementation

    /// <inheritdoc />
    public IWedaCloudService CloudService => MockCloudService;

    /// <inheritdoc />
    public ILoggerFactory LoggerFactory => MockLoggerFactory;

    /// <inheritdoc />
    public IConfiguration? Configuration => null;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeviceConfiguration> DeviceConfigs => DeviceConfigsInternal;

    /// <inheritdoc />
    public DeviceConfiguration this[string configKey] =>
        DeviceConfigsInternal.TryGetValue(configKey, out var config)
            ? config
            : throw new KeyNotFoundException($"Device configuration '{configKey}' not found. Available keys: {string.Join(", ", DeviceConfigsInternal.Keys)}");

    /// <inheritdoc />
    public IConfigurationCache ConfigurationCache => MockConfigurationCache;

    /// <inheritdoc />
    public IRecordingService? RecordingService { get; set; }

    /// <inheritdoc />
    public RecordingOptions? RecordingOptions { get; set; }

    /// <inheritdoc />
    public ILogger<T> GetLogger<T>() => MockLoggerFactory.CreateLogger<T>();

    // ===== Device Registry Convenience Methods =====

    /// <inheritdoc />
    public IDevice GetDevice(string deviceName) => DeviceRegistry.GetDevice(deviceName);

    /// <inheritdoc />
    public TDevice GetDevice<TDevice>(string deviceName) where TDevice : IDevice
        => DeviceRegistry.GetDevice<TDevice>(deviceName);

    /// <inheritdoc />
    public IDevice? FindDevice(string deviceName) => DeviceRegistry.FindDevice(deviceName);

    /// <inheritdoc />
    public TDevice? FindDevice<TDevice>(string deviceName) where TDevice : class, IDevice
        => DeviceRegistry.FindDevice<TDevice>(deviceName);

    /// <inheritdoc />
    public IReadOnlyCollection<TDevice> GetAllDevices<TDevice>() where TDevice : IDevice
        => DeviceRegistry.GetAllDevices<TDevice>();

    /// <inheritdoc />
    public ISubNodeManager SubNodeManager => MockSubNodeManager;

    /// <summary>
    /// Gets the mock SubNode manager.
    /// Use this to setup mock behaviors for SubNode operations.
    /// </summary>
    public ISubNodeManager MockSubNodeManager { get; private set; } = null!;

    public IDynamicRecordStorage? DynamicRecordStorage { get; set;}

    #endregion

    #region Helper Methods


    /// <summary>
    /// Creates a mock communication instance.
    /// This is called by DeviceBase.CreateCommunication().
    /// </summary>
    public IRequestResponseCommunication<byte[], byte[]> CreateTcpCommunication(string host, int port)
    {
        return MockCommunication;
    }

    /// <summary>
    /// Sets up default behaviors for mocks to make tests easier.
    /// </summary>
    private void SetupDefaultBehaviors()
    {
        // Default: Communication is connected
        MockCommunication.State.Returns(CommunicationState.Connected);
        MockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockCommunication.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Default: Cloud service is connected
        MockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockCloudService.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Default: Configuration cache paths
        MockConfigurationCache.CacheDirectoryPath.Returns(".weda");
        MockConfigurationCache.GetCacheFilePath(Arg.Any<SubscriptionType>()).Returns(info =>
        {
            var configType = info.Arg<SubscriptionType>();
            return configType.Value switch
            {
                "system-config" => ".weda/systemcfg.cache.json",
                "device-config" => ".weda/devicecfg.cache.json",
                "custom-config" => ".weda/customcfg.cache.json",
                _ => $".weda/{configType.Value}.cache.json"
            };
        });
        MockConfigurationCache.ExistsAsync(Arg.Any<SubscriptionType>(), Arg.Any<CancellationToken>()).Returns(false);

        // Default: SubNodeManager is initialized successfully
        // Simulates the scenario: Cloud connected, no cache, registration succeeded
        MockSubNodeManager.IsInitialized.Returns(true);
        MockSubNodeManager.SubNodeId.Returns("test-subnode-001");
        MockSubNodeManager.InitializeAsync(Arg.Any<CancellationToken>()).Returns(true);
    }

    /// <summary>
    /// Resets all mock call history.
    /// Useful when running multiple test scenarios in sequence.
    /// </summary>
    public void ResetMocks()
    {
        MockCloudService.ClearReceivedCalls();
        MockCommunication.ClearReceivedCalls();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the context.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Mock objects don't need disposal
        }

        _disposed = true;
    }

    #endregion
}
