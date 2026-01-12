namespace Weda.SubNode.Core.Configuration.Results;

/// <summary>
/// Result of applying DSP pipeline updates.
/// </summary>
public class DspPipelineUpdateResult
{
    /// <summary>
    /// List of config-based filters that were updated.
    /// </summary>
    public List<string> UpdatedConfigFilters { get; } = [];

    /// <summary>
    /// List of runtime filters that were updated.
    /// </summary>
    public List<string> UpdatedRuntimeFilters { get; } = [];

    /// <summary>
    /// List of filters that were skipped (e.g., due to type mismatch).
    /// </summary>
    public List<string> SkippedFilters { get; } = [];

    /// <summary>
    /// Total count of updated filters.
    /// </summary>
    public int TotalUpdated => UpdatedConfigFilters.Count + UpdatedRuntimeFilters.Count;
}
