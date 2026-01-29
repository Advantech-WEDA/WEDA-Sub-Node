using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.WebApi.Contracts;

namespace Weda.SubNode.WebApi.Controllers;

public class RecordingsController(IRecordingService recordingService) : ApiController
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
        
        var result = await recordingService.GetSensorsAsync(pageIndex, pageSize, cancellationToken);

        return result.Match(Ok, errors => Problem(errors));
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
}
