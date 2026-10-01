using Microsoft.Extensions.Configuration;
using Shouldly;
using Weda.SubNode.Host.Configuration;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Recording moved out of systemcfg (task #54512). A systemcfg.json that still carries a Record
/// section is detected on startup so an operator gets a migration signal, rather than silently
/// having their retention setting ignored.
/// </summary>
public class SystemCfgRecordMigrationTests
{
    private static IConfiguration ConfigWith(params (string key, string value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void HasLegacySystemCfgRecord_WhenPresent_ReturnsTrue()
    {
        var configuration = ConfigWith(("SystemConfig:Record:RetentionDays", "7"));

        SystemCfgRecordMigration.HasLegacySystemCfgRecord(configuration).ShouldBeTrue();
    }

    [Fact]
    public void HasLegacySystemCfgRecord_WhenAbsent_ReturnsFalse()
    {
        var configuration = ConfigWith(("SystemConfig:WedaNode:Url", "nats://x"));

        SystemCfgRecordMigration.HasLegacySystemCfgRecord(configuration).ShouldBeFalse();
    }
}
