namespace Weda.SubNode.Core.Configuration.Results;

/// <summary>
/// Result of replacing sensors using REPLACE mode.
/// </summary>
public class SensorReplacementResult
{
    /// <summary>
    /// List of sensors that were added (new sensors not in current config).
    /// </summary>
    public List<string> AddedSensors { get; } = [];

    /// <summary>
    /// List of sensors that were removed (in current but not in desired).
    /// </summary>
    public List<string> RemovedSensors { get; } = [];

    /// <summary>
    /// List of sensors that were updated (existing sensors with changed values).
    /// </summary>
    public List<string> UpdatedSensors { get; } = [];

    /// <summary>
    /// True if any sensors were added, removed, or updated.
    /// </summary>
    public bool HasChanges => AddedSensors.Count > 0 || RemovedSensors.Count > 0 || UpdatedSensors.Count > 0;

    /// <summary>
    /// True if DTDL needs to be regenerated (sensors added or removed).
    /// </summary>
    public bool RequiresDtdlRegeneration => AddedSensors.Count > 0 || RemovedSensors.Count > 0;
}
