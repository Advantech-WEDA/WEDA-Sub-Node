using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// SubNode-level settings container on devicecfg (parallel to subNode / deviceConfigs).
/// Broad container for whole-node settings that are remotely configurable via the
/// DeviceConfig desired pipeline; recording is the first member.
/// </summary>
public class SubNodeSettingsDto
{
    /// <summary>
    /// Recording configuration (retention, ring-buffer limits, batching).
    /// </summary>
    [JsonPropertyName("record")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubNodeRecordConfigDto? Record { get; set; }
}
