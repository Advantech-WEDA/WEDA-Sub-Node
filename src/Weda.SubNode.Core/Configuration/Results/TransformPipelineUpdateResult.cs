namespace Weda.SubNode.Core.Configuration.Results;

/// <summary>
/// Result of applying transform pipeline updates.
/// </summary>
public class TransformPipelineUpdateResult
{
    /// <summary>
    /// List of config-based transforms that were updated.
    /// </summary>
    public List<string> UpdatedConfigTransforms { get; } = [];

    /// <summary>
    /// List of runtime transforms that were updated.
    /// </summary>
    public List<string> UpdatedRuntimeTransforms { get; } = [];

    /// <summary>
    /// List of transforms that were skipped (e.g., due to type mismatch).
    /// </summary>
    public List<string> SkippedTransforms { get; } = [];

    /// <summary>
    /// Total count of updated transforms.
    /// </summary>
    public int TotalUpdated => UpdatedConfigTransforms.Count + UpdatedRuntimeTransforms.Count;
}
