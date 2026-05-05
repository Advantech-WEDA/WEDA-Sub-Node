using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace SystemAgentExample.Communication;

/// <summary>
/// Request for system metrics collection.
/// </summary>
public record SystemMetricsRequest(HashSet<string> MetricTypes);

/// <summary>
/// Communication implementation for local system resource access.
/// </summary>
public class LocalSystemCommunication : RequestResponseCommunicationBase<SystemMetricsRequest, SystemMetricsRawData>
{
    private readonly LocalSystemResourceCollector _collector;

    public LocalSystemCommunication(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings ?? new ConnectionSettings { RequestLock = false }, logger)
    {
        // LocalSystemResourceCollector initialization should never fail
        // because it gracefully handles hardware platform init failures
        _collector = new LocalSystemResourceCollector(_logger);
    }

    /// <summary>
    /// Verifies we can access system APIs.
    /// This is our "connection test" - if we can read basic system info, we're connected.
    /// </summary>
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var processorCount = Environment.ProcessorCount;
            var osVersion = Environment.OSVersion;

            _logger.LogDebug(
                "Local system communication connected. Processors: {ProcessorCount}, OS: {OSVersion}",
                processorCount,
                osVersion);

            State = CommunicationState.Connected;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to local system APIs");
            State = CommunicationState.Error;
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Disconnects from local system (just marks as disconnected).
    /// </summary>
    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == CommunicationState.Disconnected)
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("Disconnecting from local system communication");
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Discovers available system resources for sensor auto-expansion.
    /// </summary>
    public DiscoveredResources DiscoverAvailableResources() => _collector.DiscoverAvailableResources();

    /// <summary>
    /// Implements the Request-Response pattern for system metrics collection.
    /// </summary>
    /// <param name="request">System metrics request containing metric types to collect</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Raw system metrics data</returns>
    protected override async Task<SystemMetricsRawData> RequestAsyncCore(
        SystemMetricsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Collect system metrics for types: {Types}", string.Join(", ", request.MetricTypes));
            return await _collector.CollectMetricsAsync(request.MetricTypes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect system metrics for types: {Types}",
                string.Join(", ", request.MetricTypes));
            State = CommunicationState.Error;
            throw;
        }
    }
}
