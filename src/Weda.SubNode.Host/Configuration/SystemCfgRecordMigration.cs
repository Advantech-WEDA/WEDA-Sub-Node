using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Host.Configuration;

/// <summary>
/// Recording configuration moved from systemcfg to devicecfg.subNodeSettings.record. A
/// systemcfg.json that still carries a Record section is no longer read; this detects it so the
/// operator gets a migration signal instead of silently losing the setting.
/// </summary>
public static class SystemCfgRecordMigration
{
    /// <summary>
    /// True when the configuration still contains a legacy <c>SystemConfig:Record</c> section.
    /// </summary>
    public static bool HasLegacySystemCfgRecord(IConfiguration configuration)
        => configuration.GetSection($"{SystemCfg.SectionName}:{RecordingOptions.SectionName}").Exists();

    /// <summary>
    /// Logs a migration warning when a legacy <c>SystemConfig:Record</c> section is present.
    /// </summary>
    public static void WarnIfLegacyRecordPresent(IConfiguration configuration, ILogger logger)
    {
        if (HasLegacySystemCfgRecord(configuration))
        {
            logger.LogWarning(
                "systemcfg.json contains a 'Record' section, which is no longer read. Recording is now " +
                "configured through devicecfg.json 'SubNodeSettings.Record'. Move your Record settings there; " +
                "the systemcfg Record section is ignored.");
        }
    }
}
