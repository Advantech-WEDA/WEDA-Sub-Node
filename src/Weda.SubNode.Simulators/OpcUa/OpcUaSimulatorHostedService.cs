using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Simulators.OpcUa;

/// <summary>
/// Hosted service wrapper for OpcUaSimulator.
/// Manages the simulator lifecycle within the application host.
/// </summary>
public class OpcUaSimulatorHostedService : IHostedService
{
    private readonly OpcUaSimulator _simulator;
    private readonly ILogger _logger;

    public OpcUaSimulatorHostedService(
        OpcUaSimulatorConfiguration configuration,
        ILogger<OpcUaSimulator> logger)
    {
        _simulator = new OpcUaSimulator(configuration, logger: logger);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OPC-UA Simulator...");
        await _simulator.StartAsync(cancellationToken);
        _logger.LogInformation("OPC-UA Simulator started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OPC-UA Simulator...");
        await _simulator.StopAsync();
        _logger.LogInformation("OPC-UA Simulator stopped");
    }
}
