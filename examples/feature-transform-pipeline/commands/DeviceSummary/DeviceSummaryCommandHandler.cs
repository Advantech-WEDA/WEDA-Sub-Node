using ErrorOr;

using FeatureTransformPipeline.commands.DeviceSummary.Models;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

using testdevice;

namespace FeatureTransformPipeline.commands.DeviceSummary;

/// <summary>
/// Handler for "device.summary": reports user-facing device information
/// (identity, IP endpoint, sensor count, and a sensor inventory) for every
/// MyFirstDevice configured on this SubNode.
/// </summary>
[Logging(LogLevel.Information)]
public class DeviceSummaryCommandHandler
    : ICommandHandler<DeviceSummaryCommand, DeviceSummaryResult>
{
    public Task<ErrorOr<DeviceSummaryResult>> HandleAsync(
        DeviceSummaryCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<DeviceSummaryCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        cancellationToken.ThrowIfCancellationRequested();

        var devices = context.GetAllDevices<MyFirstDevice>();

        if (devices.Count == 0)
        {
            return Task.FromResult<ErrorOr<DeviceSummaryResult>>(
                DeviceSummaryResult.Error(
                    CommandStatusCode.NotFound, "No devices found", executedAt));
        }

        var entries = devices.Select(BuildEntry).ToList();

        logger.LogInformation(
            "device.summary: {DeviceCount} device(s), {SensorCount} sensor(s) total",
            entries.Count, entries.Sum(e => e.SensorCount));

        var resultData = new DeviceSummaryResultData
        {
            DeviceCount = entries.Count,
            Devices = entries
        };

        return Task.FromResult<ErrorOr<DeviceSummaryResult>>(
            DeviceSummaryResult.Success(resultData, executedAt));
    }

    private static DeviceSummaryEntry BuildEntry(MyFirstDevice device)
    {
        var config = device.Configuration;
        var subNode = config.SubNodeInfo;

        return new DeviceSummaryEntry
        {
            DeviceName = config.DeviceName,
            Host = ReadCommunication(config, "Host"),
            Port = ReadCommunicationInt(config, "Port"),
            Enabled = config.Enabled,
            Manufacturer = subNode?.Manufacturer,
            Model = subNode?.Model,
            SwVersion = subNode?.SwVersion,
            SubNodeType = subNode?.SubNodeType.ToString(),
            SensorCount = config.Sensors.Count,
            Sensors = config.Sensors
                .Select(s => $"{s.Name}|{s.SensorGroup}|{s.Report.Enabled}")
                .ToList()
        };
    }

    private static string? ReadCommunication(DeviceConfiguration config, string key) =>
        config.DeviceCommunication.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private static int? ReadCommunicationInt(DeviceConfiguration config, string key) =>
        config.DeviceCommunication.TryGetValue(key, out var value)
            && int.TryParse(value?.ToString(), out var parsed)
                ? parsed
                : null;
}
