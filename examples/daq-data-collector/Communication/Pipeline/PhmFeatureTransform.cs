using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace daq_data_collector.Communication.Pipeline;

/// <summary>
/// Telemetry transform that converts raw DAQ payload into PHM feature measurements.
/// Implements ITelemetryTransform to integrate with the telemetry pipeline.
///
/// Input: One TelemetryMeasure with ResourceId="daqraw:vibration:payload" and
///        Payload containing serialized DaqRawFrame JSON.
/// Output: 10 TelemetryMeasure instances (2 metadata + 8 PHM features).
/// </summary>
public class PhmFeatureTransform : ITelemetryTransform
{
    private readonly TimeDomainExtractor _timeDomain;
    private readonly FrequencyDomainExtractor _frequencyDomain;
    private readonly ILogger<PhmFeatureTransform> _logger;

    public string Name => "PhmFeatureTransform";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates a new PHM feature transform with configured extractors.
    /// </summary>
    /// <param name="samplingRate">Sampling rate from devicecfg.json Properties.SamplingRate</param>
    /// <param name="fftSize">FFT size from devicecfg.json Properties.FftSize</param>
    /// <param name="timeDomain">Optional custom time-domain extractor (uses default if null)</param>
    /// <param name="frequencyDomain">Optional custom frequency-domain extractor (uses default if null)</param>
    /// <param name="logger">Optional logger</param>
    public PhmFeatureTransform(
        int samplingRate,
        int fftSize,
        TimeDomainExtractor? timeDomain = null,
        FrequencyDomainExtractor? frequencyDomain = null,
        ILogger<PhmFeatureTransform>? logger = null)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        _timeDomain = timeDomain ?? new TimeDomainExtractor(samplingRate, loggerFactory.CreateLogger<TimeDomainExtractor>());
        _frequencyDomain = frequencyDomain ?? new FrequencyDomainExtractor(fftSize, loggerFactory.CreateLogger<FrequencyDomainExtractor>());
        _logger = logger ?? loggerFactory.CreateLogger<PhmFeatureTransform>();
    }

    /// <summary>
    /// Transforms raw payload into PHM feature measurements.
    /// Expects input to contain one measure with ResourceId="daqraw:vibration:payload".
    /// </summary>
    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "PhmFeatureTransform.TransformAsync: parse raw payload, extract features, emit 10 PHM measures not yet implemented.");
    }
}
