using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput.Models;

using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput;

/// <summary>
/// Handler for the "ao.get" command that reads analog output values.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IAnalogOutputReadable"/>
/// and invokes their GetAnalogOutputAsync method for each output in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all readable devices for matching output names
/// </remarks>
[Validation(typeof(GetAnalogOutputCommandValidator))]
[Logging(LogLevel.Debug)]
public class GetAnalogOutputCommandHandler : ICommandHandler<GetAnalogOutputCommand, GetAnalogOutputResult>
{
    public async Task<ErrorOr<GetAnalogOutputResult>> HandleAsync(
        GetAnalogOutputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<GetAnalogOutputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var values = new List<AnalogOutputValue>();
        var errors = new List<AnalogOutputError>();

        // Get all devices that support analog output reading
        var devices = context.GetAllDevices<IAnalogOutputReadable>();

        if (devices.Count == 0)
        {
            return GetAnalogOutputResult.Error(
                CommandStatusCode.UnsupportedCommand,
                "No devices support analog output reading",
                executedAt);
        }

        // Filter by device name if specified
        IReadOnlyCollection<IAnalogOutputReadable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = devices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                return GetAnalogOutputResult.Error(
                    CommandStatusCode.NotFound,
                    $"Device '{command.Parameters.DeviceName}' not found or does not support analog output reading",
                    executedAt);
            }

            targetDevices = [specificDevice];
        }
        else
        {
            targetDevices = devices;
        }

        // If no specific outputs requested, read all available outputs from each device
        var outputsToRead = command.Parameters.Outputs.Length > 0
            ? command.Parameters.Outputs
            : GetAllAnalogOutputNames(targetDevices);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        foreach (var outputName in outputsToRead)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var (device, value, error) = await TryReadOutputAsync(
                    targetDevices, outputName, linkedCts.Token, logger);

                if (value.HasValue)
                {
                    values.Add(new AnalogOutputValue
                    {
                        Name = outputName,
                        Value = value.Value,
                        DeviceName = device?.DeviceName
                    });
                }
                else if (error != null)
                {
                    errors.Add(new AnalogOutputError
                    {
                        Name = outputName,
                        Error = error
                    });
                }
                else if (command.Parameters.Outputs.Length > 0)
                {
                    errors.Add(new AnalogOutputError
                    {
                        Name = outputName,
                        Error = "Output not found on any device"
                    });
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return GetAnalogOutputResult.Error(
                    CommandStatusCode.Timeout,
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new GetAnalogOutputResultData
        {
            Values = values.ToArray(),
            Errors = errors.Count > 0 ? errors.ToArray() : null
        };

        if (errors.Count == 0 && values.Count > 0)
        {
            return GetAnalogOutputResult.Success(resultData, executedAt);
        }

        if (values.Count > 0 && errors.Count > 0)
        {
            return GetAnalogOutputResult.PartialSuccess(resultData, executedAt);
        }

        if (values.Count == 0 && errors.Count > 0)
        {
            return GetAnalogOutputResult.Error(
                CommandStatusCode.HardwareError,
                $"Failed to read any outputs: {errors[0].Error}",
                executedAt);
        }

        return GetAnalogOutputResult.Error(
            CommandStatusCode.HardwareError,
            "No outputs available to read",
            executedAt);
    }

    private static async Task<(IAnalogOutputReadable? device, double? value, string? error)> TryReadOutputAsync(
        IReadOnlyCollection<IAnalogOutputReadable> devices,
        string outputName,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        foreach (var device in devices)
        {
            var sensor = device.FindSensor(outputName);
            if (sensor is null)
            {
                continue;
            }

            try
            {
                var value = await device.GetAnalogOutputAsync(outputName, cancellationToken);
                if (value.HasValue)
                {
                    return (device, value.Value, null);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading {OutputName} from device {DeviceName}",
                    outputName, device.DeviceName);
                return (device, null, ex.Message);
            }
        }

        return (null, null, null);
    }

    private static string[] GetAllAnalogOutputNames(IReadOnlyCollection<IAnalogOutputReadable> devices)
    {
        return devices
            .SelectMany(d => d.Configuration.Sensors
                .Where(s => s.Name.StartsWith("ao", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name))
            .Distinct()
            .ToArray();
    }
}
