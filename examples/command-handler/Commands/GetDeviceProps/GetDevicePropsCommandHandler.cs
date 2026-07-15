using CommandHandlerExample.Commands.GetDeviceProps.Models;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

namespace CommandHandlerExample.Commands.GetDeviceProps;

/// <summary>
/// Handler for "props.get" — Dictionary&lt;string, T&gt; on both sides:
/// a dynamic option bag in, a device-name-keyed property map out.
/// </summary>
[Validation(typeof(GetDevicePropsCommandValidator))]
[Logging(LogLevel.Information)]
public class GetDevicePropsCommandHandler
    : ICommandHandler<GetDevicePropsCommand, GetDevicePropsResult>
{
    public Task<ErrorOr<GetDevicePropsResult>> HandleAsync(
        GetDevicePropsCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var options = new Dictionary<string, string>(
            command.Parameters?.Options ?? [], StringComparer.OrdinalIgnoreCase);
        options.TryGetValue("deviceName", out var deviceName);
        var includeSensors = options.TryGetValue("includeSensors", out var raw)
            && bool.TryParse(raw, out var parsed) && parsed;

        cancellationToken.ThrowIfCancellationRequested();

        var devices = context.GetAllDevices<IDevice>()
            .Where(d => string.IsNullOrWhiteSpace(deviceName)
                || d.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = devices.Count == 0
            ? GetDevicePropsResult.Error(
                CommandStatusCode.NotFound,
                string.IsNullOrWhiteSpace(deviceName)
                    ? "No devices are registered"
                    : $"Device '{deviceName}' not found",
                executedAt)
            : GetDevicePropsResult.Success(
                devices.ToDictionary(d => d.DeviceName,
                    d => BuildProps(d, includeSensors), StringComparer.OrdinalIgnoreCase),
                executedAt);

        context.GetLogger<GetDevicePropsCommandHandler>().LogInformation(
            "props.get: DeviceName={DeviceName}, IncludeSensors={IncludeSensors} -> {Count} device(s), Status={Status}",
            deviceName ?? "(all)", includeSensors, result.ResultData?.Count ?? 0, result.Status);

        return Task.FromResult<ErrorOr<GetDevicePropsResult>>(result);
    }

    private static DevicePropsEntry BuildProps(IDevice device, bool includeSensors) => new()
    {
        DeviceName = device.DeviceName,
        SubNodeType = device.SubNodeType.ToString(),
        Enabled = device.Configuration.Enabled,
        SensorCount = device.Configuration.Sensors.Count,
        Manufacturer = device.Configuration.SubNodeInfo?.Manufacturer,
        Model = device.Configuration.SubNodeInfo?.Model,
        SwVersion = device.Configuration.SubNodeInfo?.SwVersion,
        Sensors = includeSensors
            ? device.Configuration.Sensors.Select(s => new SensorPropsEntry
            {
                Name = s.Name,
                Group = s.SensorGroup.ToString(),
                Enabled = s.IsEffectivelyEnabled,
                IntervalMs = s.Report.Interval,
                Unit = s.Report.Unit
            }).ToList()
            : null
    };
}
