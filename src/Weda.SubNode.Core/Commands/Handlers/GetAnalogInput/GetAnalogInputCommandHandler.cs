using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.GetAnalogInput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogInput;

/// <summary>
/// Handler for the "ai.get" command that reads analog input values.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IAnalogInputReadable"/>
/// and invokes their GetAnalogInputAsync method for each input in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all readable devices for matching input names
/// </remarks>
[Validation(typeof(GetAnalogInputCommandValidator))]
[Logging(LogLevel.Debug)]
public class GetAnalogInputCommandHandler : ICommandHandler<GetAnalogInputCommand, GetAnalogInputResult>
{
    public async Task<ErrorOr<GetAnalogInputResult>> HandleAsync(
        GetAnalogInputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<GetAnalogInputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var values = new List<AnalogInputValue>();
        var errors = new List<AnalogInputError>();

        // Get all devices that support analog input reading
        var devices = context.GetAllDevices<IAnalogInputReadable>();

        if (devices.Count == 0)
        {
            return GetAnalogInputResult.Error(
                GetAnalogInputStatusCode.NotSupported,
                "No devices support analog input reading",
                executedAt);
        }

        // Filter by device name if specified
        IReadOnlyCollection<IAnalogInputReadable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = devices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                return GetAnalogInputResult.Error(
                    GetAnalogInputStatusCode.DeviceNotFound,
                    $"Device '{command.Parameters.DeviceName}' not found or does not support analog input reading",
                    executedAt);
            }

            targetDevices = [specificDevice];
        }
        else
        {
            targetDevices = devices;
        }

        // If no specific inputs requested, read all available inputs from each device
        var inputsToRead = command.Parameters.Inputs.Length > 0
            ? command.Parameters.Inputs
            : GetAllAnalogInputNames(targetDevices);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        foreach (var inputName in inputsToRead)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var (device, value, error) = await TryReadInputAsync(
                    targetDevices, inputName, linkedCts.Token, logger);

                if (value.HasValue)
                {
                    values.Add(new AnalogInputValue
                    {
                        Name = inputName,
                        Value = value.Value,
                        DeviceName = device?.DeviceName
                    });
                }
                else if (error != null)
                {
                    errors.Add(new AnalogInputError
                    {
                        Name = inputName,
                        Error = error
                    });
                }
                else if (command.Parameters.Inputs.Length > 0)
                {
                    errors.Add(new AnalogInputError
                    {
                        Name = inputName,
                        Error = "Input not found on any device"
                    });
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return GetAnalogInputResult.Error(
                    GetAnalogInputStatusCode.Timeout,
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new GetAnalogInputResultData
        {
            Values = values.ToArray(),
            Errors = errors.Count > 0 ? errors.ToArray() : null
        };

        if (errors.Count == 0 && values.Count > 0)
        {
            return GetAnalogInputResult.Success(resultData, executedAt);
        }

        if (values.Count > 0 && errors.Count > 0)
        {
            return GetAnalogInputResult.PartialSuccess(resultData, executedAt);
        }

        if (values.Count == 0 && errors.Count > 0)
        {
            return GetAnalogInputResult.Error(
                GetAnalogInputStatusCode.ExecutionFailed,
                $"Failed to read any inputs: {errors[0].Error}",
                executedAt);
        }

        return GetAnalogInputResult.Error(
            GetAnalogInputStatusCode.ExecutionFailed,
            "No inputs available to read",
            executedAt);
    }

    private static async Task<(IAnalogInputReadable? device, double? value, string? error)> TryReadInputAsync(
        IReadOnlyCollection<IAnalogInputReadable> devices,
        string inputName,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        foreach (var device in devices)
        {
            var sensor = device.FindSensor(inputName);
            if (sensor is null)
            {
                continue;
            }

            try
            {
                var value = await device.GetAnalogInputAsync(inputName, cancellationToken);
                if (value.HasValue)
                {
                    return (device, value.Value, null);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading {InputName} from device {DeviceName}",
                    inputName, device.DeviceName);
                return (device, null, ex.Message);
            }
        }

        return (null, null, null);
    }

    private static string[] GetAllAnalogInputNames(IReadOnlyCollection<IAnalogInputReadable> devices)
    {
        return devices
            .SelectMany(d => d.Configuration.Sensors
                .Where(s => s.Name.StartsWith("ai", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name))
            .Distinct()
            .ToArray();
    }
}
