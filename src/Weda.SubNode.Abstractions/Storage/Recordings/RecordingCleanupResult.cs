namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// What a single cleanup pass over recording storage actually did.
/// </summary>
/// <remarks>
/// Cleanup is a background job that deletes data nobody is watching. Without a per-pass tally
/// in the log there is no way to tell "the sweep ran and found nothing to delete" from "the
/// sweep never ran" — the two look identical from the file system, which is exactly how a
/// retention failure stays invisible until a disk fills up.
/// </remarks>
/// <param name="FilesScanned">Recording files examined by the pass.</param>
/// <param name="FilesDeleted">Recording files removed by the pass.</param>
/// <param name="BytesFreed">Total size of the removed files, in bytes.</param>
/// <param name="FilesFailed">Files that matched but could not be deleted (locked, permissions).</param>
public readonly record struct RecordingCleanupResult(
    int FilesScanned,
    int FilesDeleted,
    long BytesFreed,
    int FilesFailed)
{
    /// <summary>A pass that did nothing.</summary>
    public static RecordingCleanupResult Empty => default;

    /// <summary>Whether the pass removed anything.</summary>
    public bool DeletedAnything => FilesDeleted > 0;

    /// <summary>
    /// Adds two tallies so several passes (age-based, size-based, dynamic storage) can be
    /// reported as one.
    /// </summary>
    public RecordingCleanupResult Add(RecordingCleanupResult other) => new(
        FilesScanned + other.FilesScanned,
        FilesDeleted + other.FilesDeleted,
        BytesFreed + other.BytesFreed,
        FilesFailed + other.FilesFailed);

    /// <inheritdoc cref="Add"/>
    public static RecordingCleanupResult operator +(RecordingCleanupResult left, RecordingCleanupResult right)
        => left.Add(right);
}
