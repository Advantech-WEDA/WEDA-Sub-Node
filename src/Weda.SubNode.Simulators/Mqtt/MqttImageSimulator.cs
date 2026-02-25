using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Client;

namespace Weda.SubNode.Simulators.Mqtt;

public class MqttImageSimulator : IDisposable
{
    private readonly MqttImageSimulatorConfiguration _configuration;
    private readonly ILogger<MqttImageSimulator> _logger;
    private readonly byte[][] _images;
    private IMqttClient? _mqttClient;
    private CancellationTokenSource? _cts;
    private Task? _publishTask;
    private bool _disposed;

    public MqttImageSimulator(
        MqttImageSimulatorConfiguration configuration,
        ILogger<MqttImageSimulator>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<MqttImageSimulator>();
        _images = LoadEmbeddedImages();
    }

    private static byte[][] LoadEmbeddedImages()
    {
        var assembly = typeof(MqttImageSimulator).Assembly;
        var images = new List<byte[]>();

        for (var i = 0; i <= 9; i++)
        {
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($"digit_{i}.png"));

            if (resourceName == null)
                throw new InvalidOperationException($"Embedded resource digit_{i}.png not found");

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            images.Add(ms.ToArray());
        }

        return images.ToArray();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting MQTT Image Simulator → {Host}:{Port}, Topic={Topic}, Interval={Interval}s",
            _configuration.BrokerHost, _configuration.BrokerPort,
            _configuration.Topic, _configuration.IntervalSeconds);

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_configuration.BrokerHost, _configuration.BrokerPort)
            .WithClientId($"image-simulator-{Guid.NewGuid():N}"[..32])
            .Build();

        await _mqttClient.ConnectAsync(options, cancellationToken);
        _logger.LogInformation("Connected to MQTT broker");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _publishTask = Task.Run(() => PublishLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task PublishLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_configuration.IntervalSeconds * 1000, cancellationToken);

                var index = Random.Shared.Next(_images.Length);
                var imageBytes = _images[index];

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_configuration.Topic)
                    .WithPayload(imageBytes)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient!.PublishAsync(message, cancellationToken);

                _logger.LogInformation(
                    "Published digit_{Index}.png ({Size} bytes) → {Topic}",
                    index, imageBytes.Length, _configuration.Topic);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing image");
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_publishTask != null)
        {
            try { await _publishTask; }
            catch (OperationCanceledException) { }
        }

        if (_mqttClient?.IsConnected == true)
            await _mqttClient.DisconnectAsync();

        _logger.LogInformation("MQTT Image Simulator stopped");
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAsync().GetAwaiter().GetResult();
        _mqttClient?.Dispose();
        _cts?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
