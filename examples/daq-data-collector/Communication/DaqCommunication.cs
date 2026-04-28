using System.Runtime.CompilerServices;

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
        throw new NotImplementedException(
            "DaqCommunication.ConnectCoreAsync: establish connection state not yet implemented.");
    }

    /// <summary>
    /// Disconnect and clean up resources.
    /// </summary>
    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "DaqCommunication.DisconnectAsync: disconnect and cleanup not yet implemented.");
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
        throw new NotImplementedException(
            "DaqCommunication.StreamAsync: connect collector stream and yield JSON payloads not yet implemented.");
        // Note: placeholder for yield statements in implementation phase
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
