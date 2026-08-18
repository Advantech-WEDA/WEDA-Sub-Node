using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace daq_feature_proxy.Communication.Pipeline;

public class ProxyAnalysisTransform : ITelemetryTransform
{
    public string Name => "ProxyAnalysis";
    public bool Enabled { get; set; } = true;

    private readonly ILogger _logger;
    private readonly string _apiBaseEndpoint;
    private readonly string _modelId;
    private readonly int _apiTimeoutMs;
    private readonly double _anomalyThreshold;
    private readonly Dictionary<string, string> _sensorResourceIds;
    private const string ProxyVersion = "v1.0.0";
    // Outside the valid 0.0–1.0 range, clearly indicating API unavailability rather than a neutral score.
    private const double FallbackHealthScore = -1.0;

    // Built at startup from the DAQ device capability query.
    // Key: resourceShortId (5-char hex, e.g. "4dbb8"), Value: PHM Service feature key.
    // Capability query is required — no static fallback (shortIds are device-specific UUID fragments).
    private readonly IReadOnlyDictionary<string, string> _shortIdToApiMap;

    public ProxyAnalysisTransform(
        ILogger logger,
        string apiBaseEndpoint,
        string modelId,
        int apiTimeoutMs,
        double anomalyThreshold,
        Dictionary<string, string> sensorResourceIds,
        IReadOnlyDictionary<string, string> shortIdToApiMap)
    {
        _logger = logger;
        _apiBaseEndpoint = apiBaseEndpoint;
        _modelId = modelId;
        _apiTimeoutMs = apiTimeoutMs;
        _anomalyThreshold = anomalyThreshold;
        _sensorResourceIds = sensorResourceIds;
        _shortIdToApiMap = shortIdToApiMap;
    }

    public async Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var timestampMs = context.Timestamp.ToUnixTimeMilliseconds();

        try
        {
            var apiFeatures = new Dictionary<string, double>();
            foreach (var m in measures)
            {
                if (_shortIdToApiMap.TryGetValue(m.ResourceId, out var apiName))
                    apiFeatures[apiName] = Convert.ToDouble(m.Value);
            }

            var healthScore = await CallInferenceApiAsync(apiFeatures, timestampMs, cancellationToken);
            var processingTimeMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            var anomalyResult = healthScore < 0 ? "unavailable"
                : healthScore < _anomalyThreshold ? "anomaly_detected"
                : "normal";

            _logger.LogInformation(
                "Proxy analysis complete: HealthScore={HealthScore:F3}, Result={Result}, Processing={Ms}ms",
                healthScore, anomalyResult, processingTimeMs);

            return BuildOutputMeasures(healthScore, anomalyResult, processingTimeMs, timestampMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proxy analysis failed");
            return [];
        }
    }

    private async Task<double> CallInferenceApiAsync(
        Dictionary<string, double> features,
        long timestampMs,
        CancellationToken ct)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_apiTimeoutMs) };

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var requestBody = new
            {
                data = new[] { new { timestamp, features } }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var inferUrl = $"{_apiBaseEndpoint}/{_modelId}/infer";
            var response = await httpClient.PostAsync(inferUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PHM API returned {StatusCode} for model {ModelId}", response.StatusCode, _modelId);
                return FallbackHealthScore;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseContent);
            var healthScore = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("healthScore")
                .GetDouble();

            _logger.LogDebug("PHM API response: HealthScore={HealthScore:F3}", healthScore);
            return Math.Clamp(healthScore, 0.0, 1.0);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "PHM API timed out after {Ms}ms for model {ModelId}",
                _apiTimeoutMs, _modelId);
            return FallbackHealthScore;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "PHM API connection error for model {ModelId}", _modelId);
            return FallbackHealthScore;
        }
    }

    private List<TelemetryMeasure> BuildOutputMeasures(
        double healthScore, string anomalyResult, long processingTimeMs, long timestampMs)
    {
        var measures = new List<TelemetryMeasure>();

        if (_sensorResourceIds.TryGetValue("health_score", out var healthScoreId))
            measures.Add(new TelemetryMeasure
            { ResourceId = healthScoreId, Value = healthScore, Timestamp = timestampMs });

        if (_sensorResourceIds.TryGetValue("anomaly_result", out var anomalyResultId))
            measures.Add(new TelemetryMeasure
            { ResourceId = anomalyResultId, Value = anomalyResult, Timestamp = timestampMs });

        if (_sensorResourceIds.TryGetValue("processing_time_ms", out var processingId))
            measures.Add(new TelemetryMeasure
            { ResourceId = processingId, Value = processingTimeMs, Timestamp = timestampMs });

        if (_sensorResourceIds.TryGetValue("proxy_version", out var versionId))
            measures.Add(new TelemetryMeasure
            { ResourceId = versionId, Value = ProxyVersion, Timestamp = timestampMs });

        return measures;
    }
}
