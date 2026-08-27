using ErrorOr;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Commands.Gpio.Models;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;

namespace SystemAgentExample.Commands.Gpio;

/// <summary>
/// Handler for the "gpio.list" custom command: reports every GPIO pin with its
/// direction and current level, so operators can discover the pin names accepted
/// by the built-in do.set / do.get / di.get commands.
/// </summary>
[Logging(LogLevel.Information)]
public class GpioListCommandHandler : ICommandHandler<GpioListCommand, GpioListResult>
{
    public Task<ErrorOr<GpioListResult>> HandleAsync(
        GpioListCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<GpioListCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var deviceName = command.Parameters?.DeviceName;

        cancellationToken.ThrowIfCancellationRequested();

        var devices = context.GetAllDevices<IGpioPinListable>()
            .Where(d => string.IsNullOrWhiteSpace(deviceName)
                || d.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (devices.Count == 0)
        {
            var message = string.IsNullOrWhiteSpace(deviceName)
                ? "No devices expose GPIO pins"
                : $"Device '{deviceName}' not found or does not expose GPIO pins";
            return Task.FromResult<ErrorOr<GpioListResult>>(
                GpioListResult.Error(CommandStatusCode.NotFound, message, executedAt));
        }

        var inventory = devices.ToDictionary(
            d => d.DeviceName,
            d => d.ListGpioPins()
                .Select(p => new GpioPinEntry
                {
                    Name = p.Name,
                    Direction = p.Direction,
                    State = p.State,
                    SensorName = p.SensorName
                })
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

        logger.LogInformation(
            "gpio.list: {DeviceCount} device(s), {PinCount} pin(s) total",
            inventory.Count, inventory.Values.Sum(p => p.Length));

        return Task.FromResult<ErrorOr<GpioListResult>>(
            GpioListResult.Success(new GpioListResultData { Devices = inventory }, executedAt));
    }
}
