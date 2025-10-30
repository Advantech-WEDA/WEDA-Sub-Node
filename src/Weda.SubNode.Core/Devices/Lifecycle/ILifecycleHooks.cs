using ErrorOr;

namespace Weda.SubNode.Core.Devices.Lifecycle;

/// <summary>
/// Extension hooks for device lifecycle stages.
/// Derived classes implement these hooks to customize lifecycle behavior.
/// </summary>
public interface ILifecycleHooks
{
    /// <summary>
    /// Hook called during device initialization.
    /// Override to perform custom initialization logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<ErrorOr<Success>> OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    /// <summary>
    /// Hook called during device startup.
    /// Override to perform custom startup logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<ErrorOr<Success>> OnStartAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    /// <summary>
    /// Hook called during device stop.
    /// Override to perform custom stop logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<ErrorOr<Success>> OnStopAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    /// <summary>
    /// Hook called during device disposal.
    /// Override to perform custom cleanup logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<ErrorOr<Success>> OnDisposeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
