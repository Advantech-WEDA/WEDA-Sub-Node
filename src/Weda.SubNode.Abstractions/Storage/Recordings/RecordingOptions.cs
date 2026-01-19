namespace Weda.SubNode.Abstractions.Storage.Recordings;

public class RecordingOptions
{
    public const string SectionName = "Record";

    /// <summary>
    /// Directory path for storing recording data files.
    /// Supports relative paths (resolved from working directory) or absolute paths.
    /// Default is "./.weda/data/recording".
    /// </summary>
    public string StorageDirectory = "./.weda/data/recording";

    /// <summary>
    /// Minimum free disk space in megabytes.
    /// When available disk space falls below this threshold, oldest recording files
    /// are automatically deleted (Ring Buffer FIFO) to free up space.
    /// Set to 0 to disable automatic cleanup.
    /// </summary>
    public int MinFreeDiskSpaceMb { get; set; } = 128;

    /// <summary>
    /// Number of days to retain recording data.
    /// Files older than this are eligible for cleanup.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// Whether batch buffering is enabled for recording.
    /// When true, recording data points are buffered and written in batches.
    /// When false, each data point is written immediately.
    /// </summary>
    public bool BatchEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of samples to buffer before writing to storage.
    /// Only applies when BatchEnabled is true.
    /// Default is 0 samples per batch. (No batching.)
    /// </summary>
    public int BatchMaxSamples { get; set; } = 0;
}