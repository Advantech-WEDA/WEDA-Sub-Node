namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Holds the recording options currently in force and lets them be replaced at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> resolves its value once and
/// caches it, so every component that captured <c>options.Value</c> kept enforcing whatever the
/// process started with — a cloud-pushed retention change was accepted, acknowledged, and then
/// never applied. This provider is the single mutable cell that all recording components read
/// through, so one <see cref="Update"/> reaches every one of them.
/// </para>
/// <para>
/// The stored <see cref="RecordingOptions"/> instance is never mutated in place: an update
/// publishes a whole new instance, so a reader that already grabbed <see cref="Current"/>
/// keeps a coherent snapshot for the duration of its work rather than seeing fields change
/// underneath it.
/// </para>
/// </remarks>
public sealed class RecordingOptionsProvider
{
    private RecordingOptions _current;

    /// <summary>
    /// Creates a provider seeded with the given options.
    /// </summary>
    /// <param name="initial">The options to start from. Validated before being published.</param>
    /// <exception cref="ArgumentNullException"><paramref name="initial"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initial"/> holds a value outside its range.</exception>
    public RecordingOptionsProvider(RecordingOptions initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        initial.Validate();
        _current = initial;
    }

    /// <summary>
    /// The options in force right now. Each read returns a stable snapshot.
    /// </summary>
    public RecordingOptions Current => Volatile.Read(ref _current);

    /// <summary>
    /// Publishes a new set of options to every component reading through this provider.
    /// </summary>
    /// <param name="next">The replacement options. Validated before being published.</param>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="next"/> holds a value outside its range.</exception>
    public void Update(RecordingOptions next)
    {
        ArgumentNullException.ThrowIfNull(next);
        next.Validate();
        Volatile.Write(ref _current, next);
    }

    /// <summary>
    /// Replaces only the retention and ring-buffer limits, leaving every other setting as is.
    /// </summary>
    /// <param name="settings">The new limits.</param>
    /// <returns>The options now in force.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settings"/> holds a value outside its range.</exception>
    public RecordingOptions UpdateStorageSettings(RecordingStorageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var next = Current.WithStorageSettings(settings);
        Update(next);
        return next;
    }
}
