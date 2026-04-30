using System.Text.Json;

using daq_data_collector.Models;

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
        _logger.LogDebug($"[PhmFeatureTransform] TransformAsync called with {measures.Count} measures");

        var result = new List<TelemetryMeasure>();

        if (!Enabled)
            return Task.FromResult(result);

        try
        {
            // Find raw DAQ payload measure
            var rawMeasure = measures.FirstOrDefault(m => m.ResourceId == "daqraw:vibration:payload");
            if (rawMeasure == null)
            {
                _logger.LogWarning("No raw DAQ payload measure found (ResourceId='daqraw:vibration:payload')");
                return Task.FromResult(result);
            }

            // Deserialize raw frame from JSON payload
            DaqRawFrame? rawFrame = null;
            if (rawMeasure.Value is string jsonPayload)
            {
                rawFrame = JsonSerializer.Deserialize<DaqRawFrame>(jsonPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (rawMeasure.Value is JsonElement jsonElement)
            {
                rawFrame = JsonSerializer.Deserialize<DaqRawFrame>(jsonElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (rawFrame == null || rawFrame.Samples?.Length == 0)
            {
                _logger.LogWarning("Raw frame is null or empty");
                return Task.FromResult(result);
            }

            // Extract time-domain features
            var timeDomainResults = _timeDomain.Extract(rawFrame);

            // Extract frequency-domain features
            var frequencyDomainResults = _frequencyDomain.Extract(rawFrame);

            // Create PHM features object
            var phMFeatures = new DaqPhMFeatures
            {
                Timestamp = rawFrame.Timestamp,
                DeviceTime = DateTimeOffset.UtcNow,
                XAxisRMSmg = frequencyDomainResults.RMSmg,
                XAxisPeakmg = frequencyDomainResults.Peakmg,
                XAxisPeakToPeakDisplacement = frequencyDomainResults.PeakToPeakDisplacement,
                XAxisOAVelocity = frequencyDomainResults.OAVelocity,
                XAxisDeviation = timeDomainResults.Deviation,
                XAxisSkewness = timeDomainResults.Skewness,
                XAxisKurtosis = timeDomainResults.Kurtosis,
                XAxisCrestFactor = timeDomainResults.CrestFactor
            };

            // Emit 10 PHM feature measures
            result.Add(new TelemetryMeasure { ResourceId = "Timestamp_Timestamp", Value = phMFeatures.Timestamp.ToUnixTimeMilliseconds().ToString() });
            result.Add(new TelemetryMeasure { ResourceId = "Device_Time", Value = phMFeatures.DeviceTime.ToUnixTimeMilliseconds().ToString() });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_RMSmg", Value = phMFeatures.XAxisRMSmg.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_Peakmg", Value = phMFeatures.XAxisPeakmg.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_Peak-to-Peak_Displacement", Value = phMFeatures.XAxisPeakToPeakDisplacement.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_OAVelocity", Value = phMFeatures.XAxisOAVelocity.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_Deviation", Value = phMFeatures.XAxisDeviation.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_Skewness", Value = phMFeatures.XAxisSkewness.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_Kurtosis", Value = phMFeatures.XAxisKurtosis.ToString("F6") });
            result.Add(new TelemetryMeasure { ResourceId = "X-Axis_CrestFactor", Value = phMFeatures.XAxisCrestFactor.ToString("F6") });

            _logger.LogDebug("PHM feature extraction completed: 10 features emitted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PHM feature extraction");
        }

        _logger.LogInformation($"[PhmFeatureTransform] Transformed raw payload into {result.Count} PHM feature measures");

        return Task.FromResult(result);
    }
}
