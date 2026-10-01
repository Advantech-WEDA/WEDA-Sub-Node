namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Immutable snapshot of the limits that govern how long recording files are kept and how
/// much space they may occupy.
/// </summary>
/// <remarks>
/// These three values are the only recording settings that can be changed at runtime by a
/// cloud configuration update, so they travel together as one value object: a partial update
/// that left, say, <see cref="RetentionDays"/> stale while applying <see cref="MaxStorageSizeMb"/>
/// would silently enforce a policy nobody configured.
/// </remarks>
/// <param name="RetentionDays">
/// Days to keep recording files. Files dated before <c>today - RetentionDays</c> are deleted.
/// 0 disables retention-based cleanup. Valid range: 0-365.
/// </param>
/// <param name="MaxStorageSizeMb">
/// Maximum total size of the recording directory in megabytes. When exceeded, the oldest files
/// are deleted (ring-buffer FIFO) until the total is back within the limit.
/// 0 disables size-based cleanup. Valid range: 0-102400 (0-100 GB).
/// </param>
/// <param name="MinFreeDiskSpaceMb">
/// Free space that must remain available on the drive hosting the recording directory, in
/// megabytes. This is a floor on the <em>drive</em>, not a cap on the recording directory —
/// see <see cref="MaxStorageSizeMb"/> for the latter. When free space drops below it, the
/// oldest recording files are deleted until it is met again.
/// 0 disables the free-space guard. Valid range: 0-102400 (0-100 GB).
/// </param>
public sealed record RecordingStorageSettings(
    int RetentionDays,
    int MaxStorageSizeMb,
    int MinFreeDiskSpaceMb)
{
    /// <summary>Upper bound for both megabyte-valued settings (100 GB).</summary>
    public const int MaxSizeMb = 102400;

    /// <summary>Upper bound for <see cref="RetentionDays"/>.</summary>
    public const int MaxRetentionDays = 365;

    /// <summary>Whether age-based cleanup is active.</summary>
    public bool RetentionEnabled => RetentionDays > 0;

    /// <summary>Whether either ring-buffer FIFO policy is active.</summary>
    public bool SizeLimitEnabled => MaxStorageSizeMb > 0 || MinFreeDiskSpaceMb > 0;

    /// <summary>
    /// Computes the cutoff instant for age-based cleanup: recordings dated before it expire.
    /// </summary>
    /// <param name="now">The current instant, normally <c>DateTimeOffset.UtcNow</c>.</param>
    /// <returns>The cutoff, or <c>null</c> when retention is disabled.</returns>
    public DateTimeOffset? GetRetentionCutoff(DateTimeOffset now)
        => RetentionEnabled ? now.AddDays(-RetentionDays) : null;

    /// <summary>
    /// Validates every value and throws when one is out of range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its documented range.</exception>
    public void Validate()
    {
        if (RetentionDays < 0 || RetentionDays > MaxRetentionDays)
            throw new ArgumentOutOfRangeException(nameof(RetentionDays), RetentionDays, $"Must be between 0 and {MaxRetentionDays} (0=disabled)");

        if (MaxStorageSizeMb < 0 || MaxStorageSizeMb > MaxSizeMb)
            throw new ArgumentOutOfRangeException(nameof(MaxStorageSizeMb), MaxStorageSizeMb, $"Must be between 0 and {MaxSizeMb}");

        if (MinFreeDiskSpaceMb < 0 || MinFreeDiskSpaceMb > MaxSizeMb)
            throw new ArgumentOutOfRangeException(nameof(MinFreeDiskSpaceMb), MinFreeDiskSpaceMb, $"Must be between 0 and {MaxSizeMb}");
    }
}
