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
    private readonly string _rawPayloadResourceId;  // Raw sensor's actual ResourceId (UUID)
    private readonly Dictionary<string, string> _phMSensorResourceIds;  // PHM output sensor ResourceIds (name -> UUID)

    public string Name => "PhmFeatureTransform";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates a new PHM feature transform with configured extractors.
    /// </summary>
    /// <param name="samplingRate">Sampling rate from devicecfg.json Properties.SamplingRate</param>
    /// <param name="fftSize">FFT size from devicecfg.json Properties.FftSize</param>
    /// <param name="rawPayloadResourceId">ResourceId of the raw payload sensor (UUID from Sensor.ResourceId)</param>
    /// <param name="phMSensorResourceIds">Mapping of PHM output sensor names to their ResourceIds (UUID)</param>
    /// <param name="timeDomain">Optional custom time-domain extractor (uses default if null)</param>
    /// <param name="frequencyDomain">Optional custom frequency-domain extractor (uses default if null)</param>
    /// <param name="logger">Optional logger</param>
    public PhmFeatureTransform(
        int samplingRate,
        int fftSize,
        string rawPayloadResourceId,
        Dictionary<string, string>? phMSensorResourceIds = null,
        TimeDomainExtractor? timeDomain = null,
        FrequencyDomainExtractor? frequencyDomain = null,
        ILogger<PhmFeatureTransform>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(rawPayloadResourceId))
            throw new ArgumentException("rawPayloadResourceId cannot be null or empty.", nameof(rawPayloadResourceId));

        var loggerFactory = NullLoggerFactory.Instance;
        _timeDomain = timeDomain ?? new TimeDomainExtractor(samplingRate, loggerFactory.CreateLogger<TimeDomainExtractor>());
        _frequencyDomain = frequencyDomain ?? new FrequencyDomainExtractor(fftSize, loggerFactory.CreateLogger<FrequencyDomainExtractor>());
        _logger = logger ?? loggerFactory.CreateLogger<PhmFeatureTransform>();
        _rawPayloadResourceId = rawPayloadResourceId;
        _phMSensorResourceIds = phMSensorResourceIds ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Transforms raw payload into PHM feature measurements.
    /// Expects input to contain one measure with ResourceId matching the configured raw payload sensor.
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
            // Find raw DAQ payload measure using configured ResourceId
            var rawMeasure = measures.FirstOrDefault(m => m.ResourceId == _rawPayloadResourceId);
            if (rawMeasure == null)
            {
                _logger.LogWarning($"No raw DAQ payload measure found (ResourceId='{_rawPayloadResourceId}')");
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

            // Emit 10 PHM feature measures using configured ResourceIds (UUID from sensor config)
            var sensorNameMappings = new[]
            {
                ("timestamp_timestamp", phMFeatures.Timestamp.ToUnixTimeMilliseconds()),
                ("device_time", phMFeatures.DeviceTime.ToUnixTimeMilliseconds()),
                ("x_axis_rms_mg", phMFeatures.XAxisRMSmg),
                ("x_axis_peak_mg", phMFeatures.XAxisPeakmg),
                ("x_axis_peak_to_peak_displacement", phMFeatures.XAxisPeakToPeakDisplacement),
                ("x_axis_oa_velocity", phMFeatures.XAxisOAVelocity),
                ("x_axis_deviation", phMFeatures.XAxisDeviation),
                ("x_axis_skewness", phMFeatures.XAxisSkewness),
                ("x_axis_kurtosis", phMFeatures.XAxisKurtosis),
                ("x_axis_crest_factor", phMFeatures.XAxisCrestFactor)
            };

            foreach (var (sensorName, value) in sensorNameMappings)
            {
                // Use configured ResourceId if available, fallback to sensor name
                var resourceId = _phMSensorResourceIds.TryGetValue(sensorName, out var uuid)
                    ? uuid
                    : sensorName;  // Fallback for backward compatibility
                result.Add(new TelemetryMeasure { ResourceId = resourceId, Value = value });
            }

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
