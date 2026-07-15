using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput;

/// <summary>
/// Handler for the "do.set" command that sets digital output states.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IDigitalOutputControllable"/>
/// and invokes their SetDigitalOutputAsync method for each output in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all controllable devices for matching output names
/// </remarks>
[Validation(typeof(SetDigitalOutputCommandValidator))]
[Logging(LogLevel.Debug)]
[RequiresDeviceCapability(typeof(IDigitalOutputControllable))]
public class SetDigitalOutputCommandHandler : ICommandHandler<SetDigitalOutputCommand, SetDigitalOutputResult>
{
    public async Task<ErrorOr<SetDigitalOutputResult>> HandleAsync(
        SetDigitalOutputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SetDigitalOutputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Get all devices that support digital output control
        var controllableDevices = context.GetAllDevices<IDigitalOutputControllable>();

        if (controllableDevices.Count == 0)
        {
            logger.LogWarning("No devices support digital output control");
            return SetDigitalOutputResult.Error(
                CommandStatusCode.UnsupportedCommand,
                "UNSUPPORTED_COMMAND",
                "No devices support digital output control",
                executedAt);
        }

        logger.LogInformation(
            "Processing SetDigitalOutput command: {OutputCount} outputs, DeviceName={DeviceName}",
            command.Parameters.Outputs.Length, command.Parameters.DeviceName ?? "(all)");

        // Filter devices if DeviceName is specified
        IReadOnlyCollection<IDigitalOutputControllable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = controllableDevices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                logger.LogWarning("Device '{DeviceName}' not found or does not support digital output", command.Parameters.DeviceName);
                return SetDigitalOutputResult.Error(
                    CommandStatusCode.NotFound,
                    "NOT_FOUND",
                    $"Device '{command.Parameters.DeviceName}' not found or does not support digital output control",
                    executedAt);
            }

            targetDevices = [specificDevice];
        }
        else
        {
            targetDevices = controllableDevices;
        }

        // Process each output
        var results = new List<DigitalOutputOperationResult>();
        var successCount = 0;
        var failureCount = 0;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        foreach (var output in command.Parameters.Outputs)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                // Find the device that has this output
                var (device, success, error) = await TrySetOutputAsync(
                    targetDevices, output.Name, output.State, linkedCts.Token, logger);

                results.Add(new DigitalOutputOperationResult
                {
                    Name = output.Name,
                    State = output.State,
                    Success = success,
                    Error = error,
                    DeviceName = device?.DeviceName
                });

                if (success)
                {
                    successCount++;
                    logger.LogDebug("Set {OutputName}={State} on device {DeviceName}",
                        output.Name, output.State, device?.DeviceName);
                }
                else
                {
                    failureCount++;
                    logger.LogWarning("Failed to set {OutputName}={State}: {Error}",
                        output.Name, output.State, error);
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("SetDigitalOutput command timed out after {Timeout}s", command.Timeout);
                return SetDigitalOutputResult.Error(
                    CommandStatusCode.Timeout,
                    "TIMEOUT",
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new SetDigitalOutputResultData
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            Outputs = results.ToArray()
        };

        if (failureCount == 0)
        {
            logger.LogInformation("SetDigitalOutput completed: {SuccessCount} outputs set successfully", successCount);
            return SetDigitalOutputResult.Success(resultData, executedAt);
        }
        else if (successCount > 0)
        {
            logger.LogWarning("SetDigitalOutput partially completed: {SuccessCount} succeeded, {FailureCount} failed",
                successCount, failureCount);
            return SetDigitalOutputResult.PartialSuccess(resultData, executedAt);
        }
        else
        {
            logger.LogError("SetDigitalOutput failed: all {FailureCount} outputs failed", failureCount);
            return SetDigitalOutputResult.Error(
                CommandStatusCode.HardwareError,
                "HARDWARE_ERROR",
                "All digital output operations failed",
                executedAt);
        }
    }

    /// <summary>
    /// Attempts to set the output on any device that has it.
    /// </summary>
    private static async Task<(IDigitalOutputControllable? device, bool success, string? error)> TrySetOutputAsync(
        IReadOnlyCollection<IDigitalOutputControllable> devices,
        string outputName,
        bool state,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        foreach (var device in devices)
        {
            // Check if device has this sensor
            var sensor = device.FindSensor(outputName);
            if (sensor is null)
            {
                continue;
            }

            // Found the device with this output, try to set it
            try
            {
                var success = await device.SetDigitalOutputAsync(outputName, state, cancellationToken);
                if (success)
                {
                    return (device, true, null);
                }
                else
                {
                    return (device, false, "Device returned failure status");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting {OutputName} on device {DeviceName}",
                    outputName, device.DeviceName);
                return (device, false, ex.Message);
            }
        }

        // Output not found on any device
        return (null, false, $"Output '{outputName}' not found on any device");
    }
}
