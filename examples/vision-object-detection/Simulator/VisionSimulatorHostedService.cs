using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Core.Communication.Mqtt;

namespace VisionObjectDetection.Simulator;

/// <summary>
/// Optional stand-in for the Advantech YOLO container. Publishes the exact
/// <c>advantech/&lt;DEVICE_ID&gt;/vision/*</c> contract (status, meta, and a 1&#160;Hz
/// detections stream) to the local broker so the SubNode ingestion path can be
/// verified end-to-end without running the real CV stack.
///
/// <para>Enabled only when the app is started with <c>--simulate</c>.</para>
/// </summary>
public sealed class VisionSimulatorHostedService : BackgroundService
{
    private readonly string _brokerHost;
    private readonly int _brokerPort;
    private readonly string _deviceId;
    private readonly ILogger<VisionSimulatorHostedService> _logger;

    public VisionSimulatorHostedService(
        string brokerHost,
        int brokerPort,
        string deviceId,
        ILogger<VisionSimulatorHostedService> logger)
    {
        _brokerHost = brokerHost;
        _brokerPort = brokerPort;
        _deviceId = deviceId;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseTopic = $"advantech/{_deviceId}/vision";
        var communication = new MqttCommunication(_brokerHost, _brokerPort, $"vision-sim-{_deviceId}");

        try
        {
            await communication.ConnectAsync(stoppingToken);
            _logger.LogInformation(
                "Vision SIMULATOR connected to {Host}:{Port}, publishing under {Base}",
                _brokerHost, _brokerPort, baseTopic);

            await PublishAsync(communication, $"{baseTopic}/status", "\"online\"", stoppingToken);
            await PublishMetaAsync(communication, $"{baseTopic}/meta", stoppingToken);

            long frame = 0;
            // Deterministic pseudo-variation (no Random — keeps runs reproducible).
            while (!stoppingToken.IsCancellationRequested)
            {
                frame++;
                var count = 3 + (int)(frame % 5);           // 3..7 objects
                var confidence = 0.60 + (frame % 30) / 100.0; // 0.60..0.89

                var detections = new object[count];
                for (var i = 0; i < count; i++)
                {
                    detections[i] = new
                    {
                        @class = "bottle",
                        confidence = Math.Round(confidence - (i * 0.01), 4),
                        bbox = new[] { 0.5, 116.1, 344.9, 1417.2 }
                    };
                }

                var payload = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    deviceId = _deviceId,
                    frame,
                    lap = 1,
                    fps = 12.17,
                    objectCount = count,
                    classCounts = new Dictionary<string, int> { ["bottle"] = count },
                    detections
                };

                await PublishAsync(
                    communication,
                    $"{baseTopic}/detections",
                    JsonSerializer.Serialize(payload),
                    stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vision simulator failed.");
        }
        finally
        {
            try
            {
                await PublishAsync(communication, $"{baseTopic}/status", "\"offline\"", CancellationToken.None);
                await communication.DisconnectAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during simulator shutdown.");
            }
        }
    }

    private static Task PublishMetaAsync(MqttCommunication communication, string topic, CancellationToken ct)
    {
        var meta = new
        {
            model = "yolo11n.pt",
            source = "OD_bottle_2.mp4",
            confThreshold = 0.4,
            iouThreshold = 0.45,
            demoVersion = "1.1.0"
        };
        return PublishAsync(communication, topic, JsonSerializer.Serialize(meta), ct);
    }

    private static Task PublishAsync(MqttCommunication communication, string topic, string json, CancellationToken ct)
        => communication.PublishAsync(topic, Encoding.UTF8.GetBytes(json), ct);
}
