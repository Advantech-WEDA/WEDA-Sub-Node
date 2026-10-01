namespace Weda.SubNode.Abstractions.Storage.Recordings;

public class RecordingOptions
{
    public const string SectionName = "Record";

    /// <summary>
    /// Directory used for recording data files when <see cref="StorageDirectory"/> is not
    /// configured. Relative paths resolve against the project root (or the working directory
    /// when no project root is found).
    /// </summary>
    public const string DefaultStorageDirectory = "./.weda/data/recording";

    /// <summary>
    /// Default minutes between cleanup sweeps.
    /// </summary>
    /// <remarks>
    /// Hourly rather than daily: a sweep that runs once every 24 hours of uptime leaves files
    /// that arrive just after a sweep sitting a full day past their retention window, and on a
    /// device that reboots more often than once a day the only sweep that ever runs is the one
    /// at startup — which sees whatever was on disk before the device came up, and nothing
    /// written since.
    /// </remarks>
    public const int DefaultCleanupIntervalMinutes = 60;

    /// <summary>
    /// Whether recording is enabled at runtime.
    /// This is a dynamic flag that can be changed via configuration updates.
    /// Note: The RecordingService must first be created (via DeviceOptions.EnableRecording)
    /// before this flag has any effect.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory path for storing recording data files.
    /// Absolute paths are used as-is; relative paths resolve against the project root.
    /// Default is <see cref="DefaultStorageDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Read once when storage is constructed. Relocating live recording data is not a runtime
    /// operation, so a change here takes effect on the next start.
    /// </remarks>
    public string StorageDirectory { get; set; } = DefaultStorageDirectory;

    /// <summary>
    /// Minimum free disk space in megabytes.
    /// When available disk space falls below this threshold, oldest recording files
    /// are automatically deleted (Ring Buffer FIFO) to free up space.
    /// Set to 0 to disable automatic cleanup.
    /// Valid range: 0-102400 (0-100GB).
    /// </summary>
    /// <remarks>
    /// This is a floor on free space for the whole drive, not a cap on the recording
    /// directory. The cap is <see cref="MaxStorageSizeMb"/>.
    /// </remarks>
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
    /// Set to 0 to disable retention-based cleanup (files retained indefinitely).
    /// Valid range: 0-365.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// Minutes between cleanup sweeps (retention and ring-buffer FIFO).
    /// Set to 0 to disable scheduled cleanup entirely (not recommended - storage then only
    /// shrinks while data is actively being written).
    /// Valid range: 0-1440 (0-24 hours).
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = DefaultCleanupIntervalMinutes;

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
    /// Maximum time range in days allowed for batch report queries.
    /// Queries exceeding this range will be rejected to prevent DoS.
    /// Set to 0 to disable the limit (not recommended).
    /// Valid range: 0-365.
    /// Default: 30 days.
    /// </summary>
    public int MaxQueryTimeRangeDays { get; set; } = 30;

    /// <summary>
    /// Maximum value accepted for <see cref="CleanupIntervalMinutes"/> (24 hours).
    /// </summary>
    public const int MaxCleanupIntervalMinutes = 1440;

    /// <summary>
    /// The retention and ring-buffer limits currently held by these options.
    /// </summary>
    public RecordingStorageSettings StorageSettings
        => new(RetentionDays, MaxStorageSizeMb, MinFreeDiskSpaceMb);

    /// <summary>
    /// How often cleanup should sweep, or <c>null</c> when scheduled cleanup is disabled.
    /// </summary>
    public TimeSpan? CleanupInterval
        => CleanupIntervalMinutes > 0 ? TimeSpan.FromMinutes(CleanupIntervalMinutes) : null;

    /// <summary>
    /// Creates a copy of these options.
    /// </summary>
    /// <returns>A new instance holding the same values.</returns>
    public RecordingOptions Clone() => new()
    {
        Enabled = Enabled,
        StorageDirectory = StorageDirectory,
        MinFreeDiskSpaceMb = MinFreeDiskSpaceMb,
        MaxStorageSizeMb = MaxStorageSizeMb,
        RetentionDays = RetentionDays,
        CleanupIntervalMinutes = CleanupIntervalMinutes,
        BatchEnabled = BatchEnabled,
        BatchMaxSamples = BatchMaxSamples,
        FlushIntervalSeconds = FlushIntervalSeconds,
        MaxQueryTimeRangeDays = MaxQueryTimeRangeDays
    };

    /// <summary>
    /// Creates a copy of these options carrying new retention and ring-buffer limits.
    /// </summary>
    /// <param name="settings">The limits to apply.</param>
    /// <returns>A new instance; the receiver is left unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    public RecordingOptions WithStorageSettings(RecordingStorageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clone = Clone();
        clone.RetentionDays = settings.RetentionDays;
        clone.MaxStorageSizeMb = settings.MaxStorageSizeMb;
        clone.MinFreeDiskSpaceMb = settings.MinFreeDiskSpaceMb;
        return clone;
    }

    /// <summary>
    /// Validates the recording options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StorageDirectory))
            throw new ArgumentException("StorageDirectory cannot be empty", nameof(StorageDirectory));

        StorageSettings.Validate();

        if (CleanupIntervalMinutes < 0 || CleanupIntervalMinutes > MaxCleanupIntervalMinutes)
            throw new ArgumentOutOfRangeException(nameof(CleanupIntervalMinutes), $"Must be between 0 and {MaxCleanupIntervalMinutes} (0=disabled)");

        if (BatchMaxSamples < 0 || BatchMaxSamples > 10000)
            throw new ArgumentOutOfRangeException(nameof(BatchMaxSamples), "Must be between 0 and 10000");

        if (FlushIntervalSeconds < 0 || FlushIntervalSeconds > 3600)
            throw new ArgumentOutOfRangeException(nameof(FlushIntervalSeconds), "Must be between 0 and 3600");

        if (MaxQueryTimeRangeDays < 0 || MaxQueryTimeRangeDays > 365)
            throw new ArgumentOutOfRangeException(nameof(MaxQueryTimeRangeDays), "Must be between 0 and 365 (0=disabled)");
    }
}
