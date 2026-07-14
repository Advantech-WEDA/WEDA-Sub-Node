using System.Globalization;

using CommandHandlerExample.Commands.SensorRead.Models;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace CommandHandlerExample.Commands.SensorRead;

/// <summary>
/// Handler for the "sensor.read" command that reads the latest cached
/// telemetry value of a named sensor.
/// </summary>
/// <remarks>
/// Device resolution:
/// - If DeviceName is specified, only that device is searched
/// - If DeviceName is empty/null, all registered devices are searched for the sensor
///
/// The handler is discovered automatically: CommandRegistry scans the entry
/// assembly for ICommandHandler implementations and routes on the command
/// type's [DeviceCmd("sensor.read")] attribute.
/// </remarks>
[Validation(typeof(SensorReadCommandValidator))]
[Logging(LogLevel.Information)]
public class SensorReadCommandHandler : ICommandHandler<SensorReadCommand, SensorReadResult>
{
    public async Task<ErrorOr<SensorReadResult>> HandleAsync(
        SensorReadCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SensorReadCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var parameters = command.Parameters;

        logger.LogInformation(
            "Processing sensor.read command: SensorName={SensorName}, DeviceName={DeviceName}",
            parameters.SensorName, parameters.DeviceName ?? "(all)");

        var devices = ResolveTargetDevices(context, parameters.DeviceName);
        if (devices.Count == 0)
        {
            logger.LogWarning("No matching device registered for '{DeviceName}'", parameters.DeviceName ?? "(all)");
            return SensorReadResult.Error(
                CommandStatusCode.NotFound,
                parameters.DeviceName is null
                    ? "No devices are registered"
                    : $"Device '{parameters.DeviceName}' not found",
                executedAt);
        }

        // SIL2 timeout protection: bound the read by the command timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var (device, sensor) = FindSensor(devices, parameters.SensorName);
            if (device is null || sensor is null)
            {
                logger.LogWarning("Sensor '{SensorName}' not found on any target device", parameters.SensorName);
                return SensorReadResult.Error(
                    CommandStatusCode.NotFound,
                    $"Sensor '{parameters.SensorName}' not found",
                    executedAt);
            }

            var measures = await device.ReadSensorTelemetryAsync(sensor.ResourceId, linkedCts.Token);
            var latest = measures.OrderByDescending(m => m.Timestamp).FirstOrDefault();
            if (latest is null)
            {
                logger.LogWarning("No cached data available for sensor '{SensorName}'", parameters.SensorName);
                return SensorReadResult.Error(
                    CommandStatusCode.NotFound,
                    $"No data available for sensor '{parameters.SensorName}'",
                    executedAt);
            }

            logger.LogInformation(
                "sensor.read completed: {DeviceName}/{SensorName} = {Value} @ {Timestamp}",
                device.DeviceName, sensor.Name, latest.Value, latest.Timestamp);

            return SensorReadResult.Success(BuildResultData(device, sensor, latest), executedAt);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning("sensor.read command timed out after {Timeout}s", command.Timeout);
            return SensorReadResult.Error(
                CommandStatusCode.Timeout,
                $"Command execution timed out after {command.Timeout} seconds",
                executedAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "sensor.read failed for sensor '{SensorName}'", parameters.SensorName);
            return SensorReadResult.Error(
                CommandStatusCode.HardwareError,
                $"Failed to read sensor '{parameters.SensorName}': {ex.Message}",
                executedAt);
        }
    }

    /// <summary>
    /// Resolves target devices: a single named device, or all registered devices.
    /// </summary>
    private static IReadOnlyCollection<IDevice> ResolveTargetDevices(
        IWedaApplicationContext context,
        string? deviceName)
    {
        var allDevices = context.GetAllDevices<IDevice>();
        if (string.IsNullOrEmpty(deviceName))
        {
            return allDevices;
        }

        var device = allDevices.FirstOrDefault(d =>
            d.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        return device is null ? [] : [device];
    }

    /// <summary>
    /// Finds the first device that has a sensor with the given name.
    /// </summary>
    private static (IDevice? Device, Sensor? Sensor) FindSensor(
        IReadOnlyCollection<IDevice> devices,
        string sensorName)
    {
        foreach (var device in devices)
        {
            var sensor = device.FindSensor(sensorName);
            if (sensor is not null)
            {
                return (device, sensor);
            }
        }

        return (null, null);
    }

    private static SensorReadResultData BuildResultData(
        IDevice device,
        Sensor sensor,
        TelemetryMeasure measure) => new()
        {
            DeviceName = device.DeviceName,
            SensorName = sensor.Name,
            Value = ToDouble(measure.Value),
            DisplayValue = Convert.ToString(measure.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            Timestamp = measure.Timestamp,
            Unit = sensor.Report.Unit
        };

    /// <summary>
    /// Converts a telemetry value to double when numeric; returns null otherwise.
    /// </summary>
    private static double? ToDouble(object value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        short s => s,
        ushort us => us,
        byte b => b,
        sbyte sb => sb,
        decimal m => (double)m,
        bool bl => bl ? 1 : 0,
        _ => null
    };
}
