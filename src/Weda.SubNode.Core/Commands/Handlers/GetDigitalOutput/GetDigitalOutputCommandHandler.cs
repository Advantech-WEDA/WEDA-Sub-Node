using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput;

/// <summary>
/// Handler for the "do.get" command that reads digital output states.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IDigitalOutputReadable"/>
/// and invokes their GetDigitalOutputAsync method for each output in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all readable devices for matching output names
/// </remarks>
[Validation(typeof(GetDigitalOutputCommandValidator))]
[Logging(LogLevel.Debug)]
public class GetDigitalOutputCommandHandler : ICommandHandler<GetDigitalOutputCommand, GetDigitalOutputResult>
{
    public async Task<ErrorOr<GetDigitalOutputResult>> HandleAsync(
        GetDigitalOutputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<GetDigitalOutputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var values = new List<DigitalOutputValue>();
        var errors = new List<DigitalOutputError>();

        // Get all devices that support digital output reading
        var devices = context.GetAllDevices<IDigitalOutputReadable>();

        if (devices.Count == 0)
        {
            return GetDigitalOutputResult.Error(
                GetDigitalOutputStatusCode.NotSupported,
                "No devices support digital output reading",
                executedAt);
        }

        // Filter by device name if specified
        IReadOnlyCollection<IDigitalOutputReadable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = devices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                return GetDigitalOutputResult.Error(
                    GetDigitalOutputStatusCode.DeviceNotFound,
                    $"Device '{command.Parameters.DeviceName}' not found or does not support digital output reading",
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
            : GetAllDigitalOutputNames(targetDevices);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        foreach (var outputName in outputsToRead)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var (device, state, error) = await TryReadOutputAsync(
                    targetDevices, outputName, linkedCts.Token, logger);

                if (state.HasValue)
                {
                    values.Add(new DigitalOutputValue
                    {
                        Name = outputName,
                        State = state.Value,
                        DeviceName = device?.DeviceName
                    });
                }
                else if (error != null)
                {
                    errors.Add(new DigitalOutputError
                    {
                        Name = outputName,
                        Error = error
                    });
                }
                else if (command.Parameters.Outputs.Length > 0)
                {
                    errors.Add(new DigitalOutputError
                    {
                        Name = outputName,
                        Error = "Output not found on any device"
                    });
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return GetDigitalOutputResult.Error(
                    GetDigitalOutputStatusCode.Timeout,
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new GetDigitalOutputResultData
        {
            Values = values.ToArray(),
            Errors = errors.Count > 0 ? errors.ToArray() : null
        };

        if (errors.Count == 0 && values.Count > 0)
        {
            return GetDigitalOutputResult.Success(resultData, executedAt);
        }

        if (values.Count > 0 && errors.Count > 0)
        {
            return GetDigitalOutputResult.PartialSuccess(resultData, executedAt);
        }

        if (values.Count == 0 && errors.Count > 0)
        {
            return GetDigitalOutputResult.Error(
                GetDigitalOutputStatusCode.ExecutionFailed,
                $"Failed to read any outputs: {errors[0].Error}",
                executedAt);
        }

        return GetDigitalOutputResult.Error(
            GetDigitalOutputStatusCode.ExecutionFailed,
            "No outputs available to read",
            executedAt);
    }

    private static async Task<(IDigitalOutputReadable? device, bool? state, string? error)> TryReadOutputAsync(
        IReadOnlyCollection<IDigitalOutputReadable> devices,
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
                var state = await device.GetDigitalOutputAsync(outputName, cancellationToken);
                if (state.HasValue)
                {
                    return (device, state.Value, null);
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

    private static string[] GetAllDigitalOutputNames(IReadOnlyCollection<IDigitalOutputReadable> devices)
    {
        return devices
            .SelectMany(d => d.Configuration.Sensors
                .Where(s => s.Name.StartsWith("do", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name))
            .Distinct()
            .ToArray();
    }
}
