using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Tests.Fakes;
using Weda.SubNode.TestBase.Builders;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Tests for SubNodeManager - the SubNode Aggregation Root.
/// Verifies transaction semantics: all-or-nothing config updates with single aggregated report.
/// </summary>
public class SubNodeManagerTests : IAsyncDisposable
{
    private readonly IWedaCloudService _mockCloudService;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly ConnectionOptions _connectionOptions;
    private readonly ILogger<SubNodeManager> _logger;
    private SubNodeManager? _manager;
    private Func<UpdateConfigurationEvent, Task>? _capturedConfigCallback;

    public SubNodeManagerTests()
    {
        _mockCloudService = Substitute.For<IWedaCloudService>();
        _deviceRegistry = new DeviceRegistry();
        _subNodeInfo = new SubNodeInfo
        {
            Name = "TestSubNode",
            DeviceId = "test-subnode-001",
            Manufacturer = "Test",
            Model = "TestModel",
            SwVersion = "1.0.0",
            SubNodeType = SubNodeType.AdamEthernet
        };
        _connectionOptions = ConnectionOptions.WithRetries(maxRetries: 1, initialDelayMs: 10, timeoutMs: 5000);
        _logger = NullLogger<SubNodeManager>.Instance;

        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.GetOrRegisterDeviceIdAsync(Arg.Any<DeviceInfo>(), Arg.Any<CancellationToken>())
            .Returns("test-subnode-001");

        // Capture the config update callback when subscribe is called
        _mockCloudService.SubscribeConfigurationUpdatesAsync(
            Arg.Any<string>(),
            Arg.Do<Func<UpdateConfigurationEvent, Task>>(cb => _capturedConfigCallback = cb),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());

        _mockCloudService.SubscribeCommandsAsync(
            Arg.Any<string>(),
            Arg.Any<Func<ExecuteCommandEvent, Task>>(),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDisposable>());

        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private SubNodeManager CreateManager()
    {
        _manager = new SubNodeManager(
            _mockCloudService,
            _subNodeInfo,
            _connectionOptions,
            _deviceRegistry,
            _logger);
        return _manager;
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
        {
            await _manager.DisposeAsync();
        }
    }

    #region Transaction Semantics Tests

    [Fact]
    public async Task HandleDeviceConfigUpdate_WithMultipleDevices_ShouldPublishSingleAggregatedReport()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var reportPublishCount = 0;
        SubNodeConfigUpdateMessage? publishedReport = null;
        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(x =>
            {
                reportPublishCount++;
                publishedReport = x.Arg<SubNodeConfigUpdateMessage>();
            });

        // Register fake devices that pass validation and apply
        var device1 = CreateFakeDevice("device-1");
        var device2 = CreateFakeDevice("device-2");
        var device3 = CreateFakeDevice("device-3");

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);
        _deviceRegistry.Register(device3);

        // Register handlers (needed for targeting)
        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.Success(device2.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-3", _ => Task.FromResult(ConfigUpdateResult.Success(device3.Configuration, "adamEthernet")));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-2", "device-3" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert - Should publish exactly ONE aggregated report (not 3)
        reportPublishCount.ShouldBe(1, "Should publish exactly ONE aggregated report for all devices");
        publishedReport.ShouldNotBeNull();
        publishedReport!.Data?.Cfg?.Reported?.DeviceCfg?.Message?.Status.ShouldBe(ConfigUpdateStatus.Success);
    }

    [Fact]
    public async Task HandleDeviceConfigUpdate_AllDevicesSkipped_ShouldNotPublishReport()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var reportPublishCount = 0;
        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(_ => reportPublishCount++);

        // Register fake devices that return skipped
        var device1 = CreateFakeDevice("device-1")
            .WithValidationResult(ConfigUpdateValidationResult.Skipped("adamEthernet"));
        var device2 = CreateFakeDevice("device-2")
            .WithValidationResult(ConfigUpdateValidationResult.Skipped("adamEthernet"));

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);

        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.NoUpdateRequired(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.NoUpdateRequired(device2.Configuration, "adamEthernet")));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-2" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert - No report should be published when all devices are skipped
        reportPublishCount.ShouldBe(0, "Should not publish report when all devices are skipped");
    }

    [Fact]
    public async Task HandleDeviceConfigUpdate_ValidationFails_ShouldPublishInvalidStatusAndNotApply()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        SubNodeConfigUpdateMessage? publishedReport = null;
        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(x => publishedReport = x.Arg<SubNodeConfigUpdateMessage>());

        // device-1: Valid, device-2: Invalid validation
        var device1 = CreateFakeDevice("device-1");
        var device2 = CreateFakeDevice("device-2")
            .WithValidationResult(ConfigUpdateValidationResult.Invalid("adamEthernet", "Validation error for device-2"));

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);

        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.Invalid(device2.Configuration, "adamEthernet", "Error")));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-2" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert
        publishedReport.ShouldNotBeNull();
        publishedReport!.Data?.Cfg?.Reported?.DeviceCfg?.Message?.Status.ShouldBe(ConfigUpdateStatus.Invalid,
            "Should publish Invalid status when any validation fails");

        // Verify apply was NOT called on device-1 (because device-2 validation failed)
        device1.ApplyCallCount.ShouldBe(0, "device-1 should NOT have apply called when validation fails");
    }

    [Fact]
    public async Task HandleDeviceConfigUpdate_ApplyFails_ShouldRollbackAndPublishFailedStatus()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        SubNodeConfigUpdateMessage? publishedReport = null;
        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(x => publishedReport = x.Arg<SubNodeConfigUpdateMessage>());

        // device-1: Success apply, device-2: Failed apply
        var device1 = CreateFakeDevice("device-1");
        var device2 = CreateFakeDevice("device-2")
            .WithApplyResult(ConfigUpdateResult.Failed(
                CreateTestDeviceConfiguration("device-2"),
                "adamEthernet",
                "Apply failed for device-2"));

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);

        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.Failed(device2.Configuration, "adamEthernet", "Error")));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-2" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert
        publishedReport.ShouldNotBeNull();
        publishedReport!.Data?.Cfg?.Reported?.DeviceCfg?.Message?.Status.ShouldBe(ConfigUpdateStatus.Failed,
            "Should publish Failed status when any apply fails");

        // Verify device-1 was rolled back (because device-2 apply failed)
        device1.RollbackCallCount.ShouldBe(1, "device-1 should be rolled back when device-2 apply fails");
    }

    #endregion

    #region Aggregated Report Content Tests

    [Fact]
    public async Task HandleDeviceConfigUpdate_Success_AggregatedReportContainsAllDeviceConfigs()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        SubNodeConfigUpdateMessage? publishedReport = null;
        _mockCloudService.PublishConfigurationReportAsync(
            Arg.Any<SubscriptionType>(),
            Arg.Any<SubNodeConfigUpdateMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true)
            .AndDoes(x => publishedReport = x.Arg<SubNodeConfigUpdateMessage>());

        var device1 = CreateFakeDevice("device-1");
        var device2 = CreateFakeDevice("device-2");

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);

        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.Success(device2.Configuration, "adamEthernet")));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-2" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert
        publishedReport.ShouldNotBeNull();
        var deviceConfigs = publishedReport!.Data?.Cfg?.Reported?.DeviceCfg?.DeviceConfigs;
        deviceConfigs.ShouldNotBeNull();
        // Note: DeviceConfigs keyed by deviceTypeName, so both devices with same type share key
        deviceConfigs!.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region DTMI Delta Re-Upload Tests

    [Fact]
    public async Task HandleDeviceConfigUpdate_WithDtmiDelta_ShouldTriggerReUpload()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var uploadCallCount = 0;
        _mockCloudService.UploadDeviceConfigurationsAsync(
            Arg.Any<DeviceConfigurations>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr.ErrorOr<bool>>(true))
            .AndDoes(_ => uploadCallCount++);

        // Create device with DTMI delta in validation result
        var device1 = CreateFakeDevice("device-1")
            .WithValidationResult(ConfigUpdateValidationResult.Valid("adamEthernet", hasDtmiDelta: true));

        _deviceRegistry.Register(device1);

        manager.RegisterDeviceHandler("device-1", _ =>
            Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet", hasDtmiDelta: true)));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert
        uploadCallCount.ShouldBe(1, "Should trigger re-upload when DTMI delta detected");
    }

    [Fact]
    public async Task HandleDeviceConfigUpdate_WithoutDtmiDelta_ShouldNotTriggerReUpload()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var uploadCalled = false;
        _mockCloudService.UploadDeviceConfigurationsAsync(
            Arg.Any<DeviceConfigurations>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr.ErrorOr<bool>>(true))
            .AndDoes(_ => uploadCalled = true);

        var device1 = CreateFakeDevice("device-1");
        _deviceRegistry.Register(device1);

        manager.RegisterDeviceHandler("device-1", _ =>
            Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet", hasDtmiDelta: false)));

        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert
        uploadCalled.ShouldBeFalse("Should not trigger re-upload when no DTMI delta");
    }

    #endregion

    #region Handler Registration Tests

    [Fact]
    public async Task HandleDeviceConfigUpdate_OnlyTargetedDevices_ShouldOnlyProcessTargetedDevices()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var device1 = CreateFakeDevice("device-1");
        var device2 = CreateFakeDevice("device-2");
        var device3 = CreateFakeDevice("device-3");

        _deviceRegistry.Register(device1);
        _deviceRegistry.Register(device2);
        _deviceRegistry.Register(device3);

        manager.RegisterDeviceHandler("device-1", _ => Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-2", _ => Task.FromResult(ConfigUpdateResult.Success(device2.Configuration, "adamEthernet")));
        manager.RegisterDeviceHandler("device-3", _ => Task.FromResult(ConfigUpdateResult.Success(device3.Configuration, "adamEthernet")));

        // Only target device-1 and device-3
        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "device-3" });

        // Act
        _capturedConfigCallback.ShouldNotBeNull();
        await _capturedConfigCallback!(updateEvent);

        // Assert - device-2 should NOT be validated
        device1.ValidateCallCount.ShouldBe(1, "device-1 should be validated");
        device2.ValidateCallCount.ShouldBe(0, "device-2 should NOT be validated");
        device3.ValidateCallCount.ShouldBe(1, "device-3 should be validated");
    }

    [Fact]
    public async Task HandleDeviceConfigUpdate_UnregisteredDevice_ShouldNotFail()
    {
        // Arrange
        var manager = CreateManager();
        await manager.InitializeAsync();

        var device1 = CreateFakeDevice("device-1");
        _deviceRegistry.Register(device1);

        manager.RegisterDeviceHandler("device-1", _ =>
            Task.FromResult(ConfigUpdateResult.Success(device1.Configuration, "adamEthernet")));

        // Target both registered and unregistered device
        var updateEvent = CreateDeviceConfigUpdateEvent(new[] { "device-1", "unknown-device" });

        // Act & Assert - should not throw
        _capturedConfigCallback.ShouldNotBeNull();
        await Should.NotThrowAsync(async () => await _capturedConfigCallback!(updateEvent));
    }

    #endregion

    #region Helper Methods

    private DeviceConfiguration CreateTestDeviceConfiguration(string deviceName)
    {
        return DeviceConfigurationBuilder.Default()
            .WithDeviceName(deviceName)
            .WithDeviceId("test-subnode-001")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithSensors(new List<Sensor>())
            .Build();
    }

    private FakeDevice CreateFakeDevice(string deviceName)
    {
        var config = CreateTestDeviceConfiguration(deviceName);
        return new FakeDevice(config);
    }

    private UpdateConfigurationEvent CreateDeviceConfigUpdateEvent(string[] deviceNames)
    {
        var deviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in deviceNames)
        {
            deviceConfigs[name] = new SubNodeDeviceConfigDto
            {
                Sensors = new List<SubNodeSensorReportDto>()
            };
        }

        var message = new SubNodeConfigUpdateMessage
        {
            SeqId = 1,
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = new SubNodeDeviceCfgDto
                        {
                            DeviceConfigs = deviceConfigs
                        }
                    }
                }
            }
        };

        return new UpdateConfigurationEvent(
            DeviceId: "test-subnode-001",
            ConfigType: SubscriptionTypes.DeviceConfig,
            Message: message,
            Timestamp: DateTimeOffset.UtcNow);
    }

    #endregion
}
