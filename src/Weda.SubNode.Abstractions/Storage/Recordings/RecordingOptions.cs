namespace Weda.SubNode.Abstractions.Storage.Recordings;

public class RecordingOptions
{
    public const string SectionName = "Record";

    /// <summary>
    /// Whether recording is enabled at runtime.
    /// This is a dynamic flag that can be changed via configuration updates.
    /// Note: The RecordingService must first be created (via DeviceOptions.EnableRecording)
    /// before this flag has any effect.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory path for storing recording data files.
    /// Default is "./.weda/data/recording".
    /// </summary>
    public const string StorageDirectory = "./.weda/data/recording";

    /// <summary>
    /// Minimum free disk space in megabytes.
    /// When available disk space falls below this threshold, oldest recording files
    /// are automatically deleted (Ring Buffer FIFO) to free up space.
    /// Set to 0 to disable automatic cleanup.
    /// Valid range: 0-102400 (0-100GB).
    /// </summary>
    public int MinFreeDiskSpaceMb { get; set; } = 128;

    /// <summary>
    /// Maximum total storage size in megabytes for recording files.
    /// When exceeded, oldest recording files are deleted (Ring Buffer FIFO)
    /// until the total size is within the limit.
    /// Set to 0 to disable size-based cleanup.
    /// Valid range: 0-102400 (0-100GB).
    /// </summary>
    public int MaxStorageSizeMb { get; set; } = 1024;

    /// <summary>
    /// Number of days to retain recording data.
    /// Files older than this are eligible for cleanup.
    /// Valid range: 1-365.
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
    /// Set to 0 for no sample-based batching (only time-based flush).
    /// Valid range: 0-10000.
    /// </summary>
    public int BatchMaxSamples { get; set; } = 0;

    /// <summary>
    /// Interval in seconds to flush any buffered data to storage.
    /// Only applies when BatchEnabled is true.
    /// Set to 0 to disable periodic flush (not recommended).
    /// Valid range: 0-3600 (0-1 hour).
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Validates the recording options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StorageDirectory))
            throw new ArgumentException("StorageDirectory cannot be empty", nameof(StorageDirectory));

        if (MinFreeDiskSpaceMb < 0 || MinFreeDiskSpaceMb > 102400)
            throw new ArgumentOutOfRangeException(nameof(MinFreeDiskSpaceMb), "Must be between 0 and 102400");

        if (MaxStorageSizeMb < 0 || MaxStorageSizeMb > 102400)
            throw new ArgumentOutOfRangeException(nameof(MaxStorageSizeMb), "Must be between 0 and 102400");

        if (RetentionDays < 1 || RetentionDays > 365)
            throw new ArgumentOutOfRangeException(nameof(RetentionDays), "Must be between 1 and 365");

        if (BatchMaxSamples < 0 || BatchMaxSamples > 10000)
            throw new ArgumentOutOfRangeException(nameof(BatchMaxSamples), "Must be between 0 and 10000");

        if (FlushIntervalSeconds < 0 || FlushIntervalSeconds > 3600)
            throw new ArgumentOutOfRangeException(nameof(FlushIntervalSeconds), "Must be between 0 and 3600");
    }
}
