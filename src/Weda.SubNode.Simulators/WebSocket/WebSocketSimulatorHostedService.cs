using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Simulators.WebSocket;

/// <summary>
/// Hosted service wrapper for WebSocketSimulator
/// Manages the simulator lifecycle within the application host
/// </summary>
public class WebSocketSimulatorHostedService : IHostedService
{
    private readonly WebSocketSimulator _simulator;
    private readonly ILogger _logger;

    public WebSocketSimulatorHostedService(
        WebSocketSimulatorConfiguration configuration,
        ILogger<WebSocketSimulator> logger)
    {
        _simulator = new WebSocketSimulator(configuration, logger: logger);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting WebSocket Simulator...");
        await _simulator.StartAsync(cancellationToken);
        _logger.LogInformation("WebSocket Simulator started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WebSocket Simulator...");
        await _simulator.StopAsync();
        _logger.LogInformation("WebSocket Simulator stopped");
    }
}
