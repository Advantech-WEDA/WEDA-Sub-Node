using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// Unit tests for JsonConfigurationCache.
/// Tests the configuration cache mechanism for UC9868 (cloud-driven config updates).
/// Multi-Cache Design: Separate cache files per config type:
/// - .weda/systemcfg.cache.json - System configuration
/// - .weda/devicecfg.cache.json - Device configuration
/// - .weda/customcfg.cache.json - Custom configuration
/// </summary>
public class JsonConfigurationCacheTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly JsonConfigurationCache _cache;

    public JsonConfigurationCacheTests()
    {
        // Create unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"config-cache-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _cache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WhenCacheNotExists_ReturnsFalse()
    {
        // Act
        var exists = await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheExists_ReturnsTrue()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Act
        var exists = await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheFileIsEmpty_ReturnsFalse()
    {
        // Arrange
        var cachePath = _cache.GetCacheFilePath(SubscriptionTypes.DeviceConfig);
        await File.WriteAllTextAsync(cachePath, string.Empty);

        // Act
        var exists = await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_DifferentConfigTypes_AreIndependent()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Act & Assert
        (await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig)).ShouldBeTrue();
        (await _cache.ExistsAsync(SubscriptionTypes.SystemConfig)).ShouldBeFalse();
        (await _cache.ExistsAsync(SubscriptionTypes.CustomConfig)).ShouldBeFalse();
    }

    #endregion

    #region SaveRawConfigurationAsync Tests

    [Fact]
    public async Task SaveRawConfigurationAsync_CreatesFile()
    {
        // Arrange
        var message = CreateTestCloudMessage();

        // Act
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Assert
        var cachePath = _cache.GetCacheFilePath(SubscriptionTypes.DeviceConfig);
        File.Exists(cachePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveRawConfigurationAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, null!));
    }

    [Fact]
    public async Task SaveRawConfigurationAsync_PreservesAllProperties()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Report!.Enabled = false;
        desiredConfig.Sensors[0].Report!.Interval = 2000;
        desiredConfig.Periods!.ReportHealth = 10000;

        // Act
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);
        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceId.ShouldBe(message.DeviceId);
        var loadedConfig = loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Report!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[0].Report!.Interval.ShouldBe(2000);
        loadedConfig.Periods!.ReportHealth.ShouldBe(10000);
    }

    #endregion

    #region GetRawConfigurationAsync Tests

    [Fact]
    public async Task GetRawConfigurationAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var message = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        message.ShouldBeNull();
    }

    [Fact]
    public async Task GetRawConfigurationAsync_WhenCacheExists_ReturnsMessage()
    {
        // Arrange
        var original = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, original);

        // Act
        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceId.ShouldBe(original.DeviceId);
        loaded.GroupId.ShouldBe(original.GroupId);
        loaded.Cmd.ShouldBe(original.Cmd);
    }

    [Fact]
    public async Task GetRawConfigurationAsync_PreservesSensorReporturations()
    {
        // Arrange
        var original = CreateTestCloudMessage();
        var desiredConfig = original.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Report!.Enabled = false;
        desiredConfig.Sensors![1].Report!.Enabled = true;
        desiredConfig.Sensors[1].Report!.Interval = 5000;
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, original);

        // Act
        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        loaded.ShouldNotBeNull();
        var loadedConfig = loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors!.Count.ShouldBe(2);
        loadedConfig.Sensors[0].Report!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[1].Report!.Enabled.ShouldBeTrue();
        loadedConfig.Sensors[1].Report!.Interval.ShouldBe(5000);
    }

    #endregion

    #region DeleteCacheAsync Tests

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheExists_DeletesFile()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);
        var cachePath = _cache.GetCacheFilePath(SubscriptionTypes.DeviceConfig);
        File.Exists(cachePath).ShouldBeTrue();

        // Act
        await _cache.DeleteCacheAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        File.Exists(cachePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheNotExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await Should.NotThrowAsync(() => _cache.DeleteCacheAsync(SubscriptionTypes.DeviceConfig));
    }

    [Fact]
    public async Task DeleteAllCachesAsync_DeletesAllCacheFiles()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.SystemConfig, message);
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.CustomConfig, message);

        // Act
        await _cache.DeleteAllCachesAsync();

        // Assert
        (await _cache.ExistsAsync(SubscriptionTypes.SystemConfig)).ShouldBeFalse();
        (await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig)).ShouldBeFalse();
        (await _cache.ExistsAsync(SubscriptionTypes.CustomConfig)).ShouldBeFalse();
    }

    #endregion

    #region GetLastModifiedAsync Tests

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var lastModified = await _cache.GetLastModifiedAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        lastModified.ShouldBeNull();
    }

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheExists_ReturnsDateTime()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var beforeSave = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);
        var afterSave = DateTimeOffset.UtcNow.AddSeconds(1);

        // Act
        var lastModified = await _cache.GetLastModifiedAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        lastModified.ShouldNotBeNull();
        lastModified.Value.ShouldBeGreaterThan(beforeSave);
        lastModified.Value.ShouldBeLessThan(afterSave);
    }

    #endregion

    #region CacheFilePath Tests

    [Fact]
    public void CacheDirectoryPath_ReturnsConfiguredPath()
    {
        // Assert
        _cache.CacheDirectoryPath.ShouldBe(_testDirectory);
    }

    [Fact]
    public void GetCacheFilePath_ReturnsCorrectPathForEachConfigType()
    {
        // Assert
        _cache.GetCacheFilePath(SubscriptionTypes.SystemConfig)
            .ShouldBe(Path.Combine(_testDirectory, "systemcfg.cache.json"));
        _cache.GetCacheFilePath(SubscriptionTypes.DeviceConfig)
            .ShouldBe(Path.Combine(_testDirectory, "devicecfg.cache.json"));
        _cache.GetCacheFilePath(SubscriptionTypes.CustomConfig)
            .ShouldBe(Path.Combine(_testDirectory, "customcfg.cache.json"));
    }

    [Fact]
    public void DefaultCacheDirectory_IsCorrect()
    {
        // Assert
        JsonConfigurationCache.DefaultCacheDirectory.ShouldBe(".weda");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorruptCache()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple concurrent saves and reads
        for (int i = 0; i < 10; i++)
        {
            var iteration = i;
            tasks.Add(Task.Run(async () =>
            {
                var testMessage = CreateTestCloudMessage();
                testMessage.SeqId = iteration;
                await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, testMessage);
                await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Cache should be valid
        var exists = await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig);
        exists.ShouldBeTrue();

        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);
        loaded.ShouldNotBeNull();
    }

    #endregion

    #region UC9868 Scenario Tests

    [Fact]
    public async Task UC9868_DisableSensor_PersistsAcrossRestart()
    {
        // Arrange - Initial message with all sensors enabled
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Report!.Enabled = true;
        desiredConfig.Sensors[1].Report!.Enabled = true;

        // Act - Simulate cloud update disabling channel.0
        desiredConfig.Sensors[0].Report!.Enabled = false;
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Simulate restart by creating new cache instance
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert - Configuration should persist disabled state
        loadedMessage.ShouldNotBeNull();
        var loadedConfig = loadedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Report!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[1].Report!.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task UC9868_UpdateInterval_PersistsAcrossRestart()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Report!.Interval = 1000;

        // Act - Simulate cloud update changing interval
        desiredConfig.Sensors[0].Report!.Interval = 5000;
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        loadedMessage.ShouldNotBeNull();
        var loadedConfig = loadedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Report!.Interval.ShouldBe(5000);
    }

    [Fact]
    public async Task UC9868_UpdatePeriods_PersistsAcrossRestart()
    {
        // Arrange
        var message = CreateTestCloudMessage();

        // Act - Simulate cloud update changing periods
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Periods!.ReportHealth = 120000;
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        loadedMessage.ShouldNotBeNull();
        var loadedConfig = loadedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Periods!.ReportHealth.ShouldBe(120000);
    }

    [Fact]
    public async Task UC9868_ResetToAppSettings_DeleteCacheWorks()
    {
        // Arrange - Save a modified configuration
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Report!.Enabled = false;
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);

        // Act - Reset to appsettings.json by deleting cache
        await _cache.DeleteCacheAsync(SubscriptionTypes.DeviceConfig);

        // Assert
        var exists = await _cache.ExistsAsync(SubscriptionTypes.DeviceConfig);
        exists.ShouldBeFalse();

        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task UC9868_MultipleDevices_SingleCacheFile()
    {
        // Arrange - Create message with multiple devices
        var message = CreateTestCloudMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["SecondDevice"] = new SubNodeDeviceConfigDto
        {
            Enabled = true,
            SubNodeType = "adamEthernet",
            Sensors = new List<SubNodeSensorReportDto>
            {
                new SubNodeSensorReportDto
                {
                    Name = "sensor.0",
                    Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 3000 }
                }
            },
            Periods = new SubNodePeriodsDto { ReportHealth = 30000 }
        };

        // Act
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, message);
        var loaded = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);

        // Assert - Both devices should be in the single cache file
        loaded.ShouldNotBeNull();
        loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!.Count.ShouldBe(2);
        loaded.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs!.ContainsKey("TestDevice").ShouldBeTrue();
        loaded.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs!.ContainsKey("SecondDevice").ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleConfigTypes_StoreAndRetrieveIndependently()
    {
        // Arrange
        var systemMessage = CreateTestCloudMessage();
        systemMessage.DeviceId = "system-device";

        var deviceMessage = CreateTestCloudMessage();
        deviceMessage.DeviceId = "device-device";

        var customMessage = CreateTestCloudMessage();
        customMessage.DeviceId = "custom-device";

        // Act
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.SystemConfig, systemMessage);
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.DeviceConfig, deviceMessage);
        await _cache.SaveRawConfigurationAsync(SubscriptionTypes.CustomConfig, customMessage);

        // Assert
        var loadedSystem = await _cache.GetRawConfigurationAsync(SubscriptionTypes.SystemConfig);
        var loadedDevice = await _cache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig);
        var loadedCustom = await _cache.GetRawConfigurationAsync(SubscriptionTypes.CustomConfig);

        loadedSystem.ShouldNotBeNull();
        loadedSystem.DeviceId.ShouldBe("system-device");

        loadedDevice.ShouldNotBeNull();
        loadedDevice.DeviceId.ShouldBe("device-device");

        loadedCustom.ShouldNotBeNull();
        loadedCustom.DeviceId.ShouldBe("custom-device");
    }

    #endregion

    #region Helper Methods

    private static SubNodeConfigUpdateMessage CreateTestCloudMessage()
    {
        return new SubNodeConfigUpdateMessage
        {
            DeviceId = "device-123",
            GroupId = "default",
            Cmd = "updateCmd",
            SeqId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReqSeqId = "req-456",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = new SubNodeDeviceCfgDto
                        {
                            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                            {
                                ["TestDevice"] = new SubNodeDeviceConfigDto
                                {
                                    Enabled = true,
                                    DeviceType = "adamEthernet",
                                    DeviceCapabilities = new SubNodeDeviceCapabilitiesDto
                                    {
                                        Manufacturer = "Test",
                                        Model = "TestModel",
                                        SubNodeSwVersion = "0.0.1"
                                    },
                                    Communication = new Dictionary<string, object>
                                    {
                                        ["Host"] = "localhost",
                                        ["Port"] = 502
                                    },
                                    Sensors = new List<SubNodeSensorReportDto>
                                    {
                                        new SubNodeSensorReportDto
                                        {
                                            Name = "channel.0",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            }
                                        },
                                        new SubNodeSensorReportDto
                                        {
                                            Name = "channel.1",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            }
                                        }
                                    },
                                    Periods = new SubNodePeriodsDto
                                    {
                                        ReportHealth = 60000,
                                        ReportConfiguration = 1800000
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    #endregion
}
