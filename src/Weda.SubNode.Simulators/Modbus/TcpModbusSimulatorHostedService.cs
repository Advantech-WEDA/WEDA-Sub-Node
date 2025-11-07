using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Simulators.Modbus;

/// <summary>
/// Hosted service wrapper for TcpModbusSimulator
/// Manages the simulator lifecycle within the application host
/// </summary>
public class TcpModbusSimulatorHostedService : IHostedService
{
    private readonly TcpModbusSimulator _simulator;
    private readonly ILogger _logger;

    public TcpModbusSimulatorHostedService(
        TcpModbusSimulatorConfiguration configuration,
        ILogger<TcpModbusSimulator> logger)
    {
        _simulator = new TcpModbusSimulator(configuration, logger: logger);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Modbus Simulator...");
        await _simulator.StartAsync();
        _logger.LogInformation("Modbus Simulator started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Modbus Simulator...");
        await _simulator.StopAsync();
        _logger.LogInformation("Modbus Simulator stopped");
    }
}
