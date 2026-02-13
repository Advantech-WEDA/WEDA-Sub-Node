using AirQualityMonitor.Models;

using Microsoft.Extensions.Logging;


using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace AirQualityMonitor.Communication;

public record AirQualityRequest;

public class HttpCommunication(
    AirQualityClient client,
    ConnectionSettings? settings = null,
    ILogger<CommunicationBase>? logger = null) : RequestResponseCommunicationBase<AirQualityRequest, List<AirQualityRecord>?>(settings ?? new ConnectionSettings { RequestLock = false }, logger)
{
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        _logger.LogDebug("HTTP communication connected.");
        State = CommunicationState.Connected;
        return Task.FromResult(true);
    }

    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HTTP communication disconnected.");
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    protected override async Task<List<AirQualityRecord>?> RequestAsyncCore(
        AirQualityRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetAirQualityAsync(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            State = CommunicationState.Error;
            throw;
        }
    }

}