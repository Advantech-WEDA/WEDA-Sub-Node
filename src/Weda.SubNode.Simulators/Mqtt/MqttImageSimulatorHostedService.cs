using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Simulators.Mqtt;

public class MqttImageSimulatorHostedService(
    MqttImageSimulatorConfiguration configuration,
    ILogger<MqttImageSimulator> logger) : IHostedService
{
    private readonly MqttImageSimulator _simulator = new(configuration, logger);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting MQTT Image Simulator...");
        await _simulator.StartAsync(cancellationToken);
        logger.LogInformation("MQTT Image Simulator started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping MQTT Image Simulator...");
        await _simulator.StopAsync();
        logger.LogInformation("MQTT Image Simulator stopped");
    }
}