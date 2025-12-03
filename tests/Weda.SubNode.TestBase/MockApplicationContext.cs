using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
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
    /// Initializes a new instance of MockApplicationContext with default mocks.
    /// </summary>
    public MockApplicationContext()
    {
        MockCloudService = Substitute.For<IWedaCloudService>();
        MockCommunication = Substitute.For<IRequestResponseCommunication<byte[], byte[]>>();
        MockConfigurationCache = Substitute.For<IConfigurationCache>();
        MockLoggerFactory = NullLoggerFactory.Instance;
        ConnectionOptions = ConnectionOptions.Default;
        DeviceOptions = DeviceOptions.Default;
        DeviceRegistry = new DeviceRegistry();

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
        MockLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        ConnectionOptions = ConnectionOptions.Default;
        DeviceOptions = DeviceOptions.Default;
        DeviceRegistry = new DeviceRegistry();
    }

    #region IWedaApplicationContext Implementation

    /// <inheritdoc />
    public IWedaCloudService CloudService => MockCloudService;

    /// <inheritdoc />
    public ILoggerFactory LoggerFactory => MockLoggerFactory;

    /// <inheritdoc />
    public IConfiguration? Configuration => null;

    /// <inheritdoc />
    public DeviceConfiguration? DeviceConfiguration { get; set; }

    /// <inheritdoc />
    public IConfigurationCache ConfigurationCache => MockConfigurationCache;

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
