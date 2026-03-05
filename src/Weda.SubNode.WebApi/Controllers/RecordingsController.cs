using System.Text;
using Microsoft.AspNetCore.Mvc;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.WebApi.Contracts;

namespace Weda.SubNode.WebApi.Controllers;

public class RecordingsController(
    IRecordingService recordingService,
    IDeviceRegistry deviceRegistry,
    IDynamicRecordStorage? dynamicRecordStorage = null) : ApiController
{
    [HttpGet("sensors")]
    public async Task<IActionResult> GetSensors(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0)
        {
            return BadRequest("pageIndex must be >= 0");
        }
        if (pageSize < 1 || pageSize > 1000)
        {
            return BadRequest("pageSize must be between 1 and 1000");
        }

        // Get sensors from primitive storage
        var primitiveResult = await recordingService.GetSensorsAsync(pageIndex, pageSize, cancellationToken);

        if (primitiveResult.IsError)
        {
            return Problem(primitiveResult.Errors);
        }

        // Get sensors from dynamic storage
        var dynamicSensorIds = dynamicRecordStorage != null
            ? await dynamicRecordStorage.GetSensorIdsAsync(cancellationToken)
            : [];

        // Merge and deduplicate sensor IDs
        var primitiveSensorIds = primitiveResult.Value.Items.Select(s => s.ShortId).ToHashSet();
        var allSensorIds = primitiveSensorIds.Union(dynamicSensorIds).ToList();

        // Build sensor lookup from device registry
        var sensorLookup = deviceRegistry.GetAllDevices()
            .SelectMany(d => d.Configuration.Sensors)
            .ToDictionary(s => s.ShortId, s => s);

        // Map to DTOs
        var sensorDtos = allSensorIds
            .Select(shortId => sensorLookup.TryGetValue(shortId, out var sensor)
                ? RecordingSensorDto.FromSensor(sensor)
                : new RecordingSensorDto(
                    ResourceId: shortId,
                    ShortId: shortId,
                    Name: shortId,
                    Record: new SensorRecordingConfig(),
                    DeviceResourceId: "unknown"))
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        var totalCount = allSensorIds.Count;
        var pagedResult = new Weda.SubNode.Abstractions.Common.PagedResult<RecordingSensorDto>(
            sensorDtos,
            totalCount,
            pageIndex,
            pageSize);

        return Ok(pagedResult);
    }

    [HttpGet("sensors/{sensorId}")]
    public async Task<IActionResult> GetRecordings(
        string sensorId,
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var actualEnd = end ?? now;
        var actualStart = start ?? actualEnd.AddMinutes(-10);

        // Find the sensor to check its schema type
        var sensor = FindSensorByShortId(sensorId);
        var schemaType = sensor != null
            ? SchemaTypeExtensions.ParseMimeSchema(sensor.Schema)
            : null;

        // If sensor has MIME schema, use DynamicRecordStorage
        if (schemaType != null && schemaType.Value.IsMimeType() && dynamicRecordStorage != null)
        {
            return await GetDynamicRecordingsAsync(
                sensorId,
                actualStart.ToUnixTimeMilliseconds(),
                actualEnd.ToUnixTimeMilliseconds(),
                schemaType.Value,
                cancellationToken);
        }

        // If sensor not found but dynamicRecordStorage available, try reading from dynamic storage
        // This handles cases where sensor config changed but historical data exists
        if (sensor == null && dynamicRecordStorage != null)
        {
            var dynamicRecords = await dynamicRecordStorage.ReadRangeAsync(
                sensorId,
                actualStart.ToUnixTimeMilliseconds(),
                actualEnd.ToUnixTimeMilliseconds(),
                cancellationToken);

            if (dynamicRecords.Count > 0)
            {
                // Infer schema type from the first record
                var inferredSchemaType = dynamicRecords[0].SchemaType;
                return await GetDynamicRecordingsAsync(
                    sensorId,
                    actualStart.ToUnixTimeMilliseconds(),
                    actualEnd.ToUnixTimeMilliseconds(),
                    inferredSchemaType,
                    cancellationToken);
            }
        }

        // Otherwise use primitive RecordingService
        var result = await recordingService.GetRecordingsAsync(sensorId, actualStart, actualEnd, cancellationToken);

        return result.Match(
            recording => Ok(RecordingResponse.FromResult(recording)),
            errors => Problem(errors));
    }

    [HttpDelete("sensors/{sensorId}")]
    public async Task<IActionResult> DeleteSensor(string sensorId, CancellationToken cancellationToken)
    {
        var result = await recordingService.DeleteSensorAsync(sensorId, cancellationToken);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }

    [HttpDelete("sensors")]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        var result = await recordingService.DeleteAllAsync(cancellationToken);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }

    private async Task<IActionResult> GetDynamicRecordingsAsync(
        string sensorId,
        long startMs,
        long endMs,
        SchemaType schemaType,
        CancellationToken cancellationToken)
    {
        var records = await dynamicRecordStorage!.ReadRangeAsync(sensorId, startMs, endMs, cancellationToken);

        if (records.Count == 0)
        {
            return Ok(new DynamicRecordingResponse
            {
                SensorId = sensorId,
                SchemaType = schemaType.ToString(),
                Records = []
            });
        }

        var response = new DynamicRecordingResponse
        {
            SensorId = sensorId,
            SchemaType = schemaType.ToString(),
            Records = records.Select(r => new DynamicRecordItem
            {
                Timestamp = r.Timestamp,
                // For JSON, parse to JsonElement so it's not escaped; for binary, return base64 string
                Data = schemaType == SchemaType.ApplicationJson
                    ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(r.Payload.Span)
                    : Convert.ToBase64String(r.Payload.Span)
            }).ToList()
        };

        return Ok(response);
    }

    private Weda.SubNode.Abstractions.Telemetry.Sensor? FindSensorByShortId(string shortId)
    {
        foreach (var device in deviceRegistry.GetAllDevices())
        {
            var sensor = device.Configuration.Sensors
                .FirstOrDefault(s => s.ShortId == shortId);
            if (sensor != null)
                return sensor;
        }
        return null;
    }
}
