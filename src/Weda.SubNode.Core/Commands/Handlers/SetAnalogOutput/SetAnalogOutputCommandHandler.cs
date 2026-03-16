using ErrorOr;


using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Commands.Handlers.SetAnalogOutput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.SetAnalogOutput;

/// <summary>
/// Handler for the "ao.set" command that sets analog output values.
/// </summary>
/// <remarks>
/// This handler locates devices implementing <see cref="IAnalogOutputControllable"/>
/// and invokes their SetAnalogOutputAsync method for each output in the command.
///
/// Device resolution:
/// - If DeviceName is specified, only that device is used
/// - If DeviceName is empty/null, searches all controllable devices for matching output names
/// </remarks>
[Validation(typeof(SetAnalogOutputCommandValidator))]
[Logging(LogLevel.Debug)]
public class SetAnalogOutputCommandHandler : ICommandHandler<SetAnalogOutputCommand, SetAnalogOutputResult>
{
    public async Task<ErrorOr<SetAnalogOutputResult>> HandleAsync(
        SetAnalogOutputCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SetAnalogOutputCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Get all devices that support analog output control
        var controllableDevices = context.GetAllDevices<IAnalogOutputControllable>();

        if (controllableDevices.Count == 0)
        {
            logger.LogWarning("No devices support analog output control");
            return SetAnalogOutputResult.Error(
                SetAnalogOutputStatusCode.NotSupported,
                "NOT_SUPPORTED",
                "No devices support analog output control",
                executedAt);
        }

        logger.LogInformation(
            "Processing SetAnalogOutput command: {OutputCount} outputs, DeviceName={DeviceName}",
            command.Parameters.Outputs.Length, command.Parameters.DeviceName ?? "(all)");

        // Filter devices if DeviceName is specified
        IReadOnlyCollection<IAnalogOutputControllable> targetDevices;
        if (!string.IsNullOrEmpty(command.Parameters.DeviceName))
        {
            var specificDevice = controllableDevices
                .FirstOrDefault(d => d.DeviceName.Equals(command.Parameters.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (specificDevice is null)
            {
                logger.LogWarning("Device '{DeviceName}' not found or does not support analog output", command.Parameters.DeviceName);
                return SetAnalogOutputResult.Error(
                    SetAnalogOutputStatusCode.DeviceNotFound,
                    "DEVICE_NOT_FOUND",
                    $"Device '{command.Parameters.DeviceName}' not found or does not support analog output control",
                    executedAt);
            }

            targetDevices = [specificDevice];
        }
        else
        {
            targetDevices = controllableDevices;
        }

        // Process each output
        var results = new List<AnalogOutputOperationResult>();
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
                    targetDevices, output.Name, output.Value, linkedCts.Token, logger);

                results.Add(new AnalogOutputOperationResult
                {
                    Name = output.Name,
                    Value = output.Value,
                    Success = success,
                    Error = error,
                    DeviceName = device?.DeviceName
                });

                if (success)
                {
                    successCount++;
                    logger.LogDebug("Set {OutputName}={Value} on device {DeviceName}",
                        output.Name, output.Value, device?.DeviceName);
                }
                else
                {
                    failureCount++;
                    logger.LogWarning("Failed to set {OutputName}={Value}: {Error}",
                        output.Name, output.Value, error);
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("SetAnalogOutput command timed out after {Timeout}s", command.Timeout);
                return SetAnalogOutputResult.Error(
                    SetAnalogOutputStatusCode.Timeout,
                    "TIMEOUT",
                    $"Command execution timed out after {command.Timeout} seconds",
                    executedAt);
            }
        }

        var resultData = new SetAnalogOutputResultData
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            Outputs = results.ToArray()
        };

        if (failureCount == 0)
        {
            logger.LogInformation("SetAnalogOutput completed: {SuccessCount} outputs set successfully", successCount);
            return SetAnalogOutputResult.Success(resultData, executedAt);
        }
        else if (successCount > 0)
        {
            logger.LogWarning("SetAnalogOutput partially completed: {SuccessCount} succeeded, {FailureCount} failed",
                successCount, failureCount);
            return SetAnalogOutputResult.PartialSuccess(resultData, executedAt);
        }
        else
        {
            logger.LogError("SetAnalogOutput failed: all {FailureCount} outputs failed", failureCount);
            return SetAnalogOutputResult.Error(
                SetAnalogOutputStatusCode.ExecutionFailed,
                "EXECUTION_FAILED",
                "All analog output operations failed",
                executedAt);
        }
    }

    /// <summary>
    /// Attempts to set the output on any device that has it.
    /// </summary>
    private static async Task<(IAnalogOutputControllable? device, bool success, string? error)> TrySetOutputAsync(
        IReadOnlyCollection<IAnalogOutputControllable> devices,
        string outputName,
        object value,
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
                var success = await device.SetAnalogOutputAsync(outputName, value, cancellationToken);
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
