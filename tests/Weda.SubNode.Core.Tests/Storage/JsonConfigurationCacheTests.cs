using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// Unit tests for JsonConfigurationCache.
/// Tests the configuration cache mechanism for UC9868 (cloud-driven config updates).
/// Single Cache Design: The entire application uses one cache file (.weda/config.cache.json)
/// storing the raw SubNodeConfigurationUpdateMessage from cloud.
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
        var exists = await _cache.ExistsAsync();

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheExists_ReturnsTrue()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(message);

        // Act
        var exists = await _cache.ExistsAsync();

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheFileIsEmpty_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_cache.CacheFilePath, string.Empty);

        // Act
        var exists = await _cache.ExistsAsync();

        // Assert
        exists.ShouldBeFalse();
    }

    #endregion

    #region SaveRawConfigurationAsync Tests

    [Fact]
    public async Task SaveRawConfigurationAsync_CreatesFile()
    {
        // Arrange
        var message = CreateTestCloudMessage();

        // Act
        await _cache.SaveRawConfigurationAsync(message);

        // Assert
        File.Exists(_cache.CacheFilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveRawConfigurationAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _cache.SaveRawConfigurationAsync(null!));
    }

    [Fact]
    public async Task SaveRawConfigurationAsync_PreservesAllProperties()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Config!.Enabled = false;
        desiredConfig.Sensors[0].Config.Interval = 2000;
        desiredConfig.Periods!.ReportHealth = 10000;

        // Act
        await _cache.SaveRawConfigurationAsync(message);
        var loaded = await _cache.GetRawConfigurationAsync();

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceId.ShouldBe(message.DeviceId);
        var loadedConfig = loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Config!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[0].Config.Interval.ShouldBe(2000);
        loadedConfig.Periods!.ReportHealth.ShouldBe(10000);
    }

    #endregion

    #region GetRawConfigurationAsync Tests

    [Fact]
    public async Task GetRawConfigurationAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var message = await _cache.GetRawConfigurationAsync();

        // Assert
        message.ShouldBeNull();
    }

    [Fact]
    public async Task GetRawConfigurationAsync_WhenCacheExists_ReturnsMessage()
    {
        // Arrange
        var original = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(original);

        // Act
        var loaded = await _cache.GetRawConfigurationAsync();

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceId.ShouldBe(original.DeviceId);
        loaded.GroupId.ShouldBe(original.GroupId);
        loaded.Cmd.ShouldBe(original.Cmd);
    }

    [Fact]
    public async Task GetRawConfigurationAsync_PreservesSensorConfigurations()
    {
        // Arrange
        var original = CreateTestCloudMessage();
        var desiredConfig = original.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Config!.Enabled = false;
        desiredConfig.Sensors![1].Config!.Enabled = true;
        desiredConfig.Sensors[1].Config.Interval = 5000;
        await _cache.SaveRawConfigurationAsync(original);

        // Act
        var loaded = await _cache.GetRawConfigurationAsync();

        // Assert
        loaded.ShouldNotBeNull();
        var loadedConfig = loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors!.Count.ShouldBe(2);
        loadedConfig.Sensors[0].Config!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[1].Config!.Enabled.ShouldBeTrue();
        loadedConfig.Sensors[1].Config.Interval.ShouldBe(5000);
    }

    #endregion

    #region DeleteCacheAsync Tests

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheExists_DeletesFile()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        await _cache.SaveRawConfigurationAsync(message);
        File.Exists(_cache.CacheFilePath).ShouldBeTrue();

        // Act
        await _cache.DeleteCacheAsync();

        // Assert
        File.Exists(_cache.CacheFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheNotExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await Should.NotThrowAsync(() => _cache.DeleteCacheAsync());
    }

    #endregion

    #region GetLastModifiedAsync Tests

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var lastModified = await _cache.GetLastModifiedAsync();

        // Assert
        lastModified.ShouldBeNull();
    }

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheExists_ReturnsDateTime()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var beforeSave = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _cache.SaveRawConfigurationAsync(message);
        var afterSave = DateTimeOffset.UtcNow.AddSeconds(1);

        // Act
        var lastModified = await _cache.GetLastModifiedAsync();

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
    public void CacheFilePath_ReturnsCorrectPath()
    {
        // Assert
        _cache.CacheFilePath.ShouldBe(Path.Combine(_testDirectory, JsonConfigurationCache.CacheFileName));
    }

    [Fact]
    public void DefaultCacheDirectory_IsCorrect()
    {
        // Assert
        JsonConfigurationCache.DefaultCacheDirectory.ShouldBe(".weda");
    }

    [Fact]
    public void CacheFileName_IsCorrect()
    {
        // Assert
        JsonConfigurationCache.CacheFileName.ShouldBe("config.cache.json");
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
                await _cache.SaveRawConfigurationAsync(testMessage);
                await _cache.GetRawConfigurationAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Cache should be valid
        var exists = await _cache.ExistsAsync();
        exists.ShouldBeTrue();

        var loaded = await _cache.GetRawConfigurationAsync();
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
        desiredConfig.Sensors![0].Config!.Enabled = true;
        desiredConfig.Sensors[1].Config!.Enabled = true;

        // Act - Simulate cloud update disabling channel.0
        desiredConfig.Sensors[0].Config.Enabled = false;
        await _cache.SaveRawConfigurationAsync(message);

        // Simulate restart by creating new cache instance
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync();

        // Assert - Configuration should persist disabled state
        loadedMessage.ShouldNotBeNull();
        var loadedConfig = loadedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Config!.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[1].Config!.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task UC9868_UpdateInterval_PersistsAcrossRestart()
    {
        // Arrange
        var message = CreateTestCloudMessage();
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Sensors![0].Config!.Interval = 1000;

        // Act - Simulate cloud update changing interval
        desiredConfig.Sensors[0].Config.Interval = 5000;
        await _cache.SaveRawConfigurationAsync(message);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync();

        // Assert
        loadedMessage.ShouldNotBeNull();
        var loadedConfig = loadedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        loadedConfig.Sensors![0].Config!.Interval.ShouldBe(5000);
    }

    [Fact]
    public async Task UC9868_UpdatePeriods_PersistsAcrossRestart()
    {
        // Arrange
        var message = CreateTestCloudMessage();

        // Act - Simulate cloud update changing periods
        var desiredConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        desiredConfig.Periods!.ReportHealth = 120000;
        await _cache.SaveRawConfigurationAsync(message);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedMessage = await newCache.GetRawConfigurationAsync();

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
        desiredConfig.Sensors![0].Config!.Enabled = false;
        await _cache.SaveRawConfigurationAsync(message);

        // Act - Reset to appsettings.json by deleting cache
        await _cache.DeleteCacheAsync();

        // Assert
        var exists = await _cache.ExistsAsync();
        exists.ShouldBeFalse();

        var loaded = await _cache.GetRawConfigurationAsync();
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
            DeviceName = "SecondDevice",
            DeviceType = "adamEthernet",
            Sensors = new List<SubNodeSensorConfigDto>
            {
                new SubNodeSensorConfigDto
                {
                    Name = "sensor.0",
                    Config = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 3000 }
                }
            },
            Periods = new SubNodePeriodsDto { ReportHealth = 30000 }
        };

        // Act
        await _cache.SaveRawConfigurationAsync(message);
        var loaded = await _cache.GetRawConfigurationAsync();

        // Assert - Both devices should be in the single cache file
        loaded.ShouldNotBeNull();
        loaded.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!.Count.ShouldBe(2);
        loaded.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs.ContainsKey("TestDevice").ShouldBeTrue();
        loaded.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs.ContainsKey("SecondDevice").ShouldBeTrue();
    }

    #endregion

    #region Helper Methods

    private static SubNodeConfigurationUpdateMessage CreateTestCloudMessage()
    {
        return new SubNodeConfigurationUpdateMessage
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
                    Desired = new SubNodeDesiredConfig
                    {
                        SubNodeDeviceConfig = new SubNodeDeviceConfigWrapper
                        {
                            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                            {
                                ["TestDevice"] = new SubNodeDeviceConfigDto
                                {
                                    Enabled = true,
                                    DeviceName = "TestDevice",
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
                                    Sensors = new List<SubNodeSensorConfigDto>
                                    {
                                        new SubNodeSensorConfigDto
                                        {
                                            Name = "channel.0",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Config = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            }
                                        },
                                        new SubNodeSensorConfigDto
                                        {
                                            Name = "channel.1",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Config = new SubNodeSensorRuntimeConfigDto
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
