using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalInput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalInput;

/// <summary>
/// Handler for the "di.get" command that reads digital input states.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IDigitalInputReadable"/>
/// and invokes their GetDigitalInputAsync method for each input in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all readable devices for matching input names
/// </remarks>
[Validation(typeof(GetDigitalInputCommandValidator))]
[Logging(LogLevel.Debug)]
public class GetDigitalInputCommandHandler : ICommandHandler<GetDigitalInputCommand, GetDigitalInputResult>
{
    public async Task<ErrorOr<GetDigitalInputResult>> HandleAsync(
        GetDigitalInputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<GetDigitalInputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var values = new List<DigitalInputValue>();
        var errors = new List<DigitalInputError>();

        // Get all devices that support digital input reading
        var devices = context.GetAllDevices<IDigitalInputReadable>();

        if (devices.Count == 0)
        {
            return GetDigitalInputResult.Error(
                GetDigitalInputStatusCode.NotSupported,
                "No devices support digital input reading",
                executedAt);
        }

        // Filter by device name if specified
        IReadOnlyCollection<IDigitalInputReadable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = devices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                return GetDigitalInputResult.Error(
                    GetDigitalInputStatusCode.DeviceNotFound,
                    $"Device '{command.Parameters.DeviceName}' not found or does not support digital input reading",
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
            : GetAllDigitalInputNames(targetDevices);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        foreach (var inputName in inputsToRead)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var (device, state, error) = await TryReadInputAsync(
                    targetDevices, inputName, linkedCts.Token, logger);

                if (state.HasValue)
                {
                    values.Add(new DigitalInputValue
                    {
                        Name = inputName,
                        State = state.Value,
                        DeviceName = device?.DeviceName
                    });
                }
                else if (error != null)
                {
                    errors.Add(new DigitalInputError
                    {
                        Name = inputName,
                        Error = error
                    });
                }
                else if (command.Parameters.Inputs.Length > 0)
                {
                    errors.Add(new DigitalInputError
                    {
                        Name = inputName,
                        Error = "Input not found on any device"
                    });
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return GetDigitalInputResult.Error(
                    GetDigitalInputStatusCode.Timeout,
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new GetDigitalInputResultData
        {
            Values = values.ToArray(),
            Errors = errors.Count > 0 ? errors.ToArray() : null
        };

        if (errors.Count == 0 && values.Count > 0)
        {
            return GetDigitalInputResult.Success(resultData, executedAt);
        }

        if (values.Count > 0 && errors.Count > 0)
        {
            return GetDigitalInputResult.PartialSuccess(resultData, executedAt);
        }

        if (values.Count == 0 && errors.Count > 0)
        {
            return GetDigitalInputResult.Error(
                GetDigitalInputStatusCode.ExecutionFailed,
                $"Failed to read any inputs: {errors[0].Error}",
                executedAt);
        }

        return GetDigitalInputResult.Error(
            GetDigitalInputStatusCode.ExecutionFailed,
            "No inputs available to read",
            executedAt);
    }

    private static async Task<(IDigitalInputReadable? device, bool? state, string? error)> TryReadInputAsync(
        IReadOnlyCollection<IDigitalInputReadable> devices,
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
                var state = await device.GetDigitalInputAsync(inputName, cancellationToken);
                if (state.HasValue)
                {
                    return (device, state.Value, null);
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

    private static string[] GetAllDigitalInputNames(IReadOnlyCollection<IDigitalInputReadable> devices)
    {
        return devices
            .SelectMany(d => d.Configuration.Sensors
                .Where(s => s.Name.StartsWith("di", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name))
            .Distinct()
            .ToArray();
    }
}
