using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace daq_data_collector.Communication;

/// <summary>
/// Communication layer for DAQ data collection using streaming pattern.
/// Wraps Advantech.Edge.Daq streaming as a bidirectional stream of raw frames.
///
/// StreamAsync : consumes the DaqCollector async enumerable, yields DaqRawFrame
///               objects as serialized JSON payloads (object → string conversion).
/// </summary>
public class DaqCommunication : StreamingCommunicationBase<object, object>
{
    private readonly DaqCollector _collector;

    public DaqCommunication(
        DaqCollector collector,
        ILogger<DaqCommunication> logger)
        : base(new ConnectionSettings { }, logger)
    {
        _collector = collector;
    }

    /// <summary>
    /// Establish connection (transition state to Connected).
    /// Verifies that DAQ modules are available before establishing connection.
    /// </summary>
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify that DAQ modules are available
            if (!_collector.VerifyDaqModulesAvailable())
            {
                _logger.LogError("DaqCommunication connection failed: No DAQ modules available.");
                return Task.FromResult(false);
            }

            _logger.LogInformation("DaqCommunication connection established (state transition only).");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DaqCommunication connection.");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Disconnect and clean up resources.
    /// </summary>
    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DaqCommunication disconnected.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Streams raw vibration frames from DaqCollector, yielding them as
    /// serialized JSON payloads in the async enumerable.
    /// </summary>
    public override IAsyncEnumerable<object> StreamAsync(
        IAsyncEnumerable<object> requests,
        CancellationToken cancellationToken = default)
    {
        return StreamAsyncImpl(requests, cancellationToken);
    }

    private async IAsyncEnumerable<object> StreamAsyncImpl(
        IAsyncEnumerable<object> requests,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting DaqCommunication stream (consuming DaqCollector).");

        await foreach (var frame in _collector.StreamFramesAsync(cancellationToken))
        {
            _logger.LogInformation("DaqCommunication received DaqRawFrame");

            // Serialize DaqRawFrame to JSON string
            var json = JsonSerializer.Serialize(frame, new JsonSerializerOptions { WriteIndented = false });

            _logger.LogInformation("DaqCommunication yielding JSON payload (size: {size} bytes).", json.Length);

            yield return json;
        }

        _logger.LogInformation("DaqCommunication stream completed normally.");
    }
}
