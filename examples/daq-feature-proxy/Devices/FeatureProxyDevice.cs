using System.Text.Json;
using daq_feature_proxy.Communication;
using daq_feature_proxy.Communication.Pipeline;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Devices;

namespace daq_feature_proxy.Devices;

public class FeatureProxyDevice : DeviceBase
{
    private NatsClient? _natsClient;

    public FeatureProxyDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey]) { }

    public FeatureProxyDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, new NullParser()) { }

    public override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken ct = default)
        => Task.FromResult(new List<TelemetryMeasure>());

    protected override Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds, CancellationToken cancellationToken)
        => Task.FromResult(new IntervalGroupReadResult([], TimeSpan.Zero));

    protected override async Task OnAfterInitializeAsync(CancellationToken ct)
    {
        var props = Configuration.Properties;

        var apiEndpoint = ReadProp(props, "PhmApiEndpoint", "http://172.16.9.17:8000/api/v1/models");
        var modelId = ReadProp(props, "PhmApiModelId", "");
        var timeoutMs = ReadPropInt(props, "PhmApiTimeoutMs", 5000);
        var threshold = ReadPropDouble(props, "PhmAnomalyThreshold", 0.3);
        var daqDataCollectorSubNodeName = ReadProp(props, "DaqDataCollectorSubNodeName", "");

        if (string.IsNullOrEmpty(modelId))
        {
            _logger.LogError("PhmApiModelId is not configured — feature proxy will not start");
            await base.OnAfterInitializeAsync(ct);
            return;
        }

        if (string.IsNullOrEmpty(daqDataCollectorSubNodeName))
        {
            _logger.LogError("DaqDataCollectorSubNodeName is not configured — feature proxy will not start");
            await base.OnAfterInitializeAsync(ct);
            return;
        }

        var sensorResourceIds = Configuration.Sensors
            .Where(s => !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.ResourceId))
            .ToDictionary(s => s.Name, s => s.ResourceId);

        _natsClient = await InitializeNatsClientAsync(ct);
        if (_natsClient == null)
        {
            _logger.LogError("Failed to initialize NATS client — feature proxy will not receive data");
            await base.OnAfterInitializeAsync(ct);
            return;
        }

        // Step 1: Query DAQ device capabilities to obtain deviceId and shortId→name map.
        var capClient = new DeviceCapabilityClient(_natsClient, _logger);
        var capability = await capClient.QueryCapabilityAsync(daqDataCollectorSubNodeName, ct);
        if (capability is null)
        {
            _logger.LogError("Capability query failed — feature proxy will not start");
            await base.OnAfterInitializeAsync(ct);
            return;
        }

        // Step 2: Derive subscription topic and build shortId → API key map.
        var subject = $"eco1j.weda.{capability.DeviceId}.telemetry";
        var shortIdToApi = BuildShortIdToApiMap(capability.ShortIdToNameMap);

        _logger.LogInformation(
            "Capability resolved: DeviceId={DeviceId}, Topic={Topic}, {Count} feature sensors mapped",
            capability.DeviceId, subject, shortIdToApi.Count);

        // Step 3: Inject map into transform and start subscription.
        ITelemetryTransform transform = new ProxyAnalysisTransform(
            _logger, apiEndpoint, modelId,
            timeoutMs, threshold,
            sensorResourceIds, shortIdToApi);

        var handler = new FeatureSubscriptionHandler(
            _natsClient, subject, transform, _logger,
            async (measures, token) => await SendTelemetryAsync(measures, token));

        _ = handler.StartAsync(ct);

        _logger.LogInformation(
            "PHM Feature Proxy initialized: Subject={Subject}, ModelId={ModelId}, Endpoint={Endpoint}",
            subject, modelId, apiEndpoint);

        await base.OnAfterInitializeAsync(ct);
    }

    // Convention table: DAQ sensor name (snake_case) → PHM Service API key.
    // Sensors returned by capability query that are not in this table are skipped.
    private static readonly IReadOnlyDictionary<string, string> FeatureConventionTable =
        new Dictionary<string, string>
        {
            ["x_axis_rms_mg"] = "X-Axis_RMSmg",
            ["x_axis_peak_mg"] = "X-Axis_Peakmg",
            ["x_axis_oa_velocity"] = "X-Axis_OAVelocity",
            ["x_axis_deviation"] = "X-Axis_Deviation",
            ["x_axis_skewness"] = "X-Axis_Skewness",
            ["x_axis_kurtosis"] = "X-Axis_Kurtosis",
            ["x_axis_crest_factor"] = "X-Axis_CrestFactor",
            ["y_axis_rms_mg"] = "Y-Axis_RMSmg",
            ["y_axis_peak_mg"] = "Y-Axis_Peakmg",
            ["y_axis_oa_velocity"] = "Y-Axis_OAVelocity",
            ["y_axis_deviation"] = "Y-Axis_Deviation",
            ["y_axis_skewness"] = "Y-Axis_Skewness",
            ["y_axis_kurtosis"] = "Y-Axis_Kurtosis",
            ["y_axis_crest_factor"] = "Y-Axis_CrestFactor",
            ["z_axis_rms_mg"] = "Z-Axis_RMSmg",
            ["z_axis_peak_mg"] = "Z-Axis_Peakmg",
            ["z_axis_oa_velocity"] = "Z-Axis_OAVelocity",
            ["z_axis_deviation"] = "Z-Axis_Deviation",
            ["z_axis_skewness"] = "Z-Axis_Skewness",
            ["z_axis_kurtosis"] = "Z-Axis_Kurtosis",
            ["z_axis_crest_factor"] = "Z-Axis_CrestFactor",
        };

    // Converts shortId→name map (from capability) to shortId→API key using the convention table.
    private IReadOnlyDictionary<string, string> BuildShortIdToApiMap(
        IReadOnlyDictionary<string, string> shortIdToName)
    {
        var map = new Dictionary<string, string>();
        foreach (var (shortId, name) in shortIdToName)
        {
            if (FeatureConventionTable.TryGetValue(name, out var apiName))
                map[shortId] = apiName;
            else
                _logger.LogWarning(
                    "Sensor '{Name}' (shortId={ShortId}) is not in the convention table; skipping",
                    name, shortId);
        }
        return map;
    }

    private async Task<NatsClient?> InitializeNatsClientAsync(CancellationToken ct)
    {
        try
        {
            var systemConfigPath = Path.Combine(AppContext.BaseDirectory, "systemcfg.json");

            string natsUrl = "nats://127.0.0.1:4224";
            string username = "advantech_nats";
            string password = "3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48";

            if (File.Exists(systemConfigPath))
            {
                var config = JsonDocument.Parse(File.ReadAllText(systemConfigPath)).RootElement;
                if (config.TryGetProperty("WedaNode", out var wedaNode))
                {
                    natsUrl = $"nats://{wedaNode.GetProperty("Url").GetString()}";
                    username = wedaNode.GetProperty("Username").GetString() ?? username;
                    password = wedaNode.GetProperty("Password").GetString() ?? password;
                }
            }

            var natsOpts = NatsOpts.Default with
            {
                Url = natsUrl,
                AuthOpts = new NatsAuthOpts { Username = username, Password = password }
            };

            var client = new NatsClient(natsOpts);
            _logger.LogInformation("NATS client initialized: {Url}", natsUrl);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize NATS client");
            return null;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string ReadProp(Dictionary<string, object> props, string key, string defaultValue)
        => props.TryGetValue(key, out var v) && v?.ToString() is { Length: > 0 } s ? s : defaultValue;

    private static int ReadPropInt(Dictionary<string, object> props, string key, int defaultValue)
        => props.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var r) ? r : defaultValue;

    private static double ReadPropDouble(Dictionary<string, object> props, string key, double defaultValue)
        => props.TryGetValue(key, out var v) && double.TryParse(v?.ToString(), out var r) ? r : defaultValue;

    // ── Null infrastructure ────────────────────────────────────────────────────

    private sealed class NullCommunication : ICommunication
    {
        public ConnectionSettings Settings { get; } = new();
        public CommunicationState State => CommunicationState.Connected;
        public bool IsConnected => true;

#pragma warning disable CS0067
        public event EventHandler<ConnectionStateChangedEvent>? StateChanged;
#pragma warning restore CS0067

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class NullParser : IProtocolParserCore
    {
        public ICommunication Communication { get; } = new NullCommunication();
    }
}
