namespace Weda.SubNode.Core.Configuration.Results;

/// <summary>
/// Summary of all pipeline updates applied to a device configuration.
/// </summary>
public class PipelineUpdateSummary
{
    /// <summary>
    /// DSP pipeline update results by sensor name.
    /// </summary>
    public Dictionary<string, DspPipelineUpdateResult> DspResults { get; } = [];

    /// <summary>
    /// Transform pipeline update results by sensor name.
    /// </summary>
    public Dictionary<string, TransformPipelineUpdateResult> TransformResults { get; } = [];

    /// <summary>
    /// Total count of sensors that had DSP updates.
    /// </summary>
    public int TotalDspSensorsUpdated => DspResults.Count;

    /// <summary>
    /// Total count of sensors that had transform updates.
    /// </summary>
    public int TotalTransformSensorsUpdated => TransformResults.Count;
}
