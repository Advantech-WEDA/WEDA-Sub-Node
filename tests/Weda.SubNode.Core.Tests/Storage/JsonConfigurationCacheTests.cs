using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// Unit tests for JsonConfigurationCache.
/// Tests the configuration cache mechanism for UC9868 (cloud-driven config updates).
/// Multi-device support: Each device has its own cache file in .weda/{DeviceName}.config.json
/// </summary>
public class JsonConfigurationCacheTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly JsonConfigurationCache _cache;
    private const string TestDeviceName = "TestDevice";

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
        var exists = await _cache.ExistsAsync(TestDeviceName);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheExists_ReturnsTrue()
    {
        // Arrange
        var config = CreateTestConfiguration();
        await _cache.SaveConfigurationAsync(config);

        // Act
        var exists = await _cache.ExistsAsync(TestDeviceName);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenCacheFileIsEmpty_ReturnsFalse()
    {
        // Arrange
        var cacheFilePath = _cache.GetCacheFilePath(TestDeviceName);
        await File.WriteAllTextAsync(cacheFilePath, string.Empty);

        // Act
        var exists = await _cache.ExistsAsync(TestDeviceName);

        // Assert
        exists.ShouldBeFalse();
    }

    #endregion

    #region SaveConfigurationAsync Tests

    [Fact]
    public async Task SaveConfigurationAsync_CreatesFile()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act
        await _cache.SaveConfigurationAsync(config);

        // Assert
        var cacheFilePath = _cache.GetCacheFilePath(TestDeviceName);
        File.Exists(cacheFilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _cache.SaveConfigurationAsync(null!));
    }

    [Fact]
    public async Task SaveConfigurationAsync_PreservesAllProperties()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.Sensors[0].Config.Enabled = false;
        config.Sensors[0].Config.Interval = 2000;
        config.Periods.ReportHealth = 10000;

        // Act
        await _cache.SaveConfigurationAsync(config);
        var loaded = await _cache.GetConfigurationAsync(TestDeviceName);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceName.ShouldBe(config.DeviceName);
        loaded.Sensors[0].Config.Enabled.ShouldBeFalse();
        loaded.Sensors[0].Config.Interval.ShouldBe(2000);
        loaded.Periods.ReportHealth.ShouldBe(10000);
    }

    #endregion

    #region GetConfigurationAsync Tests

    [Fact]
    public async Task GetConfigurationAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var config = await _cache.GetConfigurationAsync(TestDeviceName);

        // Assert
        config.ShouldBeNull();
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenCacheExists_ReturnsConfiguration()
    {
        // Arrange
        var original = CreateTestConfiguration();
        await _cache.SaveConfigurationAsync(original);

        // Act
        var loaded = await _cache.GetConfigurationAsync(TestDeviceName);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.DeviceName.ShouldBe(original.DeviceName);
        loaded.DeviceType.ShouldBe(original.DeviceType);
    }

    [Fact]
    public async Task GetConfigurationAsync_PreservesSensorConfigurations()
    {
        // Arrange
        var original = CreateTestConfiguration();
        original.Sensors[0].Config.Enabled = false;
        original.Sensors[1].Config.Enabled = true;
        original.Sensors[1].Config.Interval = 5000;
        await _cache.SaveConfigurationAsync(original);

        // Act
        var loaded = await _cache.GetConfigurationAsync(TestDeviceName);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.Sensors.Count.ShouldBe(2);
        loaded.Sensors[0].Config.Enabled.ShouldBeFalse();
        loaded.Sensors[1].Config.Enabled.ShouldBeTrue();
        loaded.Sensors[1].Config.Interval.ShouldBe(5000);
    }

    #endregion

    #region DeleteCacheAsync Tests

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheExists_DeletesFile()
    {
        // Arrange
        var config = CreateTestConfiguration();
        await _cache.SaveConfigurationAsync(config);
        var cacheFilePath = _cache.GetCacheFilePath(TestDeviceName);
        File.Exists(cacheFilePath).ShouldBeTrue();

        // Act
        await _cache.DeleteCacheAsync(TestDeviceName);

        // Assert
        File.Exists(cacheFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCacheAsync_WhenCacheNotExists_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await Should.NotThrowAsync(() => _cache.DeleteCacheAsync(TestDeviceName));
    }

    #endregion

    #region GetLastModifiedAsync Tests

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheNotExists_ReturnsNull()
    {
        // Act
        var lastModified = await _cache.GetLastModifiedAsync(TestDeviceName);

        // Assert
        lastModified.ShouldBeNull();
    }

    [Fact]
    public async Task GetLastModifiedAsync_WhenCacheExists_ReturnsDateTime()
    {
        // Arrange
        var config = CreateTestConfiguration();
        var beforeSave = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _cache.SaveConfigurationAsync(config);
        var afterSave = DateTimeOffset.UtcNow.AddSeconds(1);

        // Act
        var lastModified = await _cache.GetLastModifiedAsync(TestDeviceName);

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
    public void GetCacheFilePath_ReturnsCorrectPath()
    {
        // Act
        var filePath = _cache.GetCacheFilePath(TestDeviceName);

        // Assert
        filePath.ShouldBe(Path.Combine(_testDirectory, $"{TestDeviceName}{JsonConfigurationCache.CacheFileExtension}"));
    }

    [Fact]
    public void DefaultCacheDirectory_IsCorrect()
    {
        // Assert
        JsonConfigurationCache.DefaultCacheDirectory.ShouldBe(".weda");
    }

    [Fact]
    public void CacheFileExtension_IsCorrect()
    {
        // Assert
        JsonConfigurationCache.CacheFileExtension.ShouldBe(".config.json");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorruptCache()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple concurrent saves and reads for different devices
        for (int i = 0; i < 10; i++)
        {
            var deviceName = $"Device-{i}";
            tasks.Add(Task.Run(async () =>
            {
                var testConfig = CreateTestConfiguration();
                testConfig.DeviceName = deviceName;
                await _cache.SaveConfigurationAsync(testConfig);
                await _cache.GetConfigurationAsync(deviceName);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All caches should be valid
        for (int i = 0; i < 10; i++)
        {
            var deviceName = $"Device-{i}";
            var exists = await _cache.ExistsAsync(deviceName);
            exists.ShouldBeTrue();

            var loaded = await _cache.GetConfigurationAsync(deviceName);
            loaded.ShouldNotBeNull();
            loaded.DeviceName.ShouldBe(deviceName);
        }
    }

    #endregion

    #region UC9868 Scenario Tests

    [Fact]
    public async Task UC9868_DisableSensor_PersistsAcrossRestart()
    {
        // Arrange - Initial configuration with all sensors enabled
        var config = CreateTestConfiguration();
        config.Sensors[0].Config.Enabled = true;
        config.Sensors[1].Config.Enabled = true;

        // Act - Simulate cloud update disabling channel.0
        config.Sensors[0].Config.Enabled = false;
        await _cache.SaveConfigurationAsync(config);

        // Simulate restart by creating new cache instance
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedConfig = await newCache.GetConfigurationAsync(TestDeviceName);

        // Assert - Configuration should persist disabled state
        loadedConfig.ShouldNotBeNull();
        loadedConfig.Sensors[0].Config.Enabled.ShouldBeFalse();
        loadedConfig.Sensors[1].Config.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task UC9868_UpdateInterval_PersistsAcrossRestart()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.Sensors[0].Config.Interval = 1000;

        // Act - Simulate cloud update changing interval
        config.Sensors[0].Config.Interval = 5000;
        await _cache.SaveConfigurationAsync(config);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedConfig = await newCache.GetConfigurationAsync(TestDeviceName);

        // Assert
        loadedConfig.ShouldNotBeNull();
        loadedConfig.Sensors[0].Config.Interval.ShouldBe(5000);
    }

    [Fact]
    public async Task UC9868_UpdatePeriods_PersistsAcrossRestart()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act - Simulate cloud update changing periods
        config.Periods.ReportHealth = 120000;
        config.Periods.PollCommands = 2000;
        await _cache.SaveConfigurationAsync(config);

        // Simulate restart
        var newCache = new JsonConfigurationCache(
            _testDirectory,
            NullLogger<JsonConfigurationCache>.Instance);

        var loadedConfig = await newCache.GetConfigurationAsync(TestDeviceName);

        // Assert
        loadedConfig.ShouldNotBeNull();
        loadedConfig.Periods.ReportHealth.ShouldBe(120000);
        loadedConfig.Periods.PollCommands.ShouldBe(2000);
    }

    [Fact]
    public async Task UC9868_ResetToAppSettings_DeleteCacheWorks()
    {
        // Arrange - Save a modified configuration
        var config = CreateTestConfiguration();
        config.Sensors[0].Config.Enabled = false;
        await _cache.SaveConfigurationAsync(config);

        // Act - Reset to appsettings.json by deleting cache
        await _cache.DeleteCacheAsync(TestDeviceName);

        // Assert
        var exists = await _cache.ExistsAsync(TestDeviceName);
        exists.ShouldBeFalse();

        var loaded = await _cache.GetConfigurationAsync(TestDeviceName);
        loaded.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private static DeviceConfiguration CreateTestConfiguration()
    {
        return new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            DeviceType = DeviceType.AdamEthernet,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Test",
                Model = "TestModel",
                SubNodeSwVersion = "0.0.1",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502
            },
            Sensors = new List<Sensor>
            {
                new Sensor
                {
                    Name = "channel.0",
                    ResourceId = "channel-0",
                    Dtmi = "dtmi:test:sensor;1",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>(),
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Interval = 1000
                    }
                },
                new Sensor
                {
                    Name = "channel.1",
                    ResourceId = "channel-1",
                    Dtmi = "dtmi:test:sensor;1",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>(),
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Interval = 1000
                    }
                }
            },
            Periods = new BackgroundTaskPeriods
            {
                ReportHealth = 60000,
                PollCommands = 1000
            }
        };
    }

    #endregion
}
