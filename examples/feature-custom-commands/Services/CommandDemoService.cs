using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Commands;

namespace CommandHandlerExample.Services;

/// <summary>
/// Demo hosted service that dispatches a synthetic "sensor.read" command
/// through the SDK command pipeline shortly after startup.
/// </summary>
/// <remarks>
/// In production the command arrives from the cloud over NATS and is routed by
/// DeviceAgentClient into the same CommandDispatcher used here. Mock cloud has
/// no command transport, so this service simulates the incoming message to
/// demonstrate the full pipeline: DTDL validation → deserialization →
/// auto-ack → validation/logging behaviors → handler → command response.
/// </remarks>
public sealed class CommandDemoService : BackgroundService
{
    /// <summary>
    /// Delay before dispatching, so the device has polled the simulator
    /// at least once and the telemetry cache is populated.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);

    private const string RespTopic = "demo.subnode.cmd.rsp";

    private readonly IServiceProvider _services;
    private readonly ILogger<CommandDemoService> _logger;

    public CommandDemoService(IServiceProvider services, ILogger<CommandDemoService> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            var registry = _services.GetRequiredService<CommandRegistry>();
            var context = _services.GetRequiredService<IWedaApplicationContext>();
            var dispatcher = new CommandDispatcher(registry, context);

            _logger.LogInformation(
                "Dispatching demo commands (registered commands: [{Commands}])",
                string.Join(", ", registry.RegisteredCommands));

            await DispatchAsync(dispatcher, "sensor.read", seqId: 1, new
            {
                deviceCmd = "sensor.read",
                timeout = 10,
                respTopic = RespTopic,
                parameters = new { sensorName = "temperature_sensor" }
            }, stoppingToken);

            // Map-typed parameters: "tags" is a Dictionary<string, string>
            // emitted as a DTDL Map schema in the command Interface.
            await DispatchAsync(dispatcher, "tag.set", seqId: 2, new
            {
                deviceCmd = "tag.set",
                timeout = 10,
                respTopic = RespTopic,
                parameters = new
                {
                    sensorName = "temperature_sensor",
                    tags = new Dictionary<string, string>
                    {
                        ["location"] = "line-3",
                        ["zone"] = "assembly",
                        ["owner"] = "demo"
                    }
                }
            }, stoppingToken);

            // Dictionary<string, T> on both sides: the "options" input is a
            // dynamic map (string → string) and the result data is a map
            // keyed by device name (string → DevicePropsEntry).
            await DispatchAsync(dispatcher, "props.get", seqId: 3, new
            {
                deviceCmd = "props.get",
                timeout = 10,
                respTopic = RespTopic,
                parameters = new
                {
                    options = new Dictionary<string, string>
                    {
                        ["includeSensors"] = "true"
                    }
                }
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down before the demo ran — nothing to report.
        }
        catch (Exception ex)
        {
            // A failed demo must not take down the host; report and stay idle.
            _logger.LogError(ex, "Demo command dispatch failed unexpectedly");
        }
    }

    /// <summary>
    /// Wraps the payload in the same envelope the cloud would publish for a
    /// device command, dispatches it, and logs the outcome.
    /// </summary>
    private async Task DispatchAsync(
        CommandDispatcher dispatcher,
        string commandName,
        ulong seqId,
        object dataPayload,
        CancellationToken cancellationToken)
    {
        var message = new CommandMessage
        {
            Cmd = "deviceCmd",
            SeqId = seqId,
            ReqSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = JsonSerializer.SerializeToElement(dataPayload)
        };

        var result = await dispatcher.DispatchAsync(message, cancellationToken);

        if (result.IsError)
        {
            _logger.LogError(
                "Demo command '{CommandName}' failed: {Errors}",
                commandName,
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
            return;
        }

        _logger.LogInformation(
            "Demo command '{CommandName}' succeeded: {Result}",
            commandName,
            JsonSerializer.Serialize(result.Value));
    }
}
