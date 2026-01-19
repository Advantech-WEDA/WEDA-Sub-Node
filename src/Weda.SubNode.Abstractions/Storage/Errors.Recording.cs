using ErrorOr;

namespace Weda.SubNode.Abstractions.Storage;

public static partial class Errors
{
    public static class Recording
    {
        public static Error SensorNotFound(string sensorId) => Error.NotFound(
            code: "Recording.SensorNotFound",
            description: $"Sensor '{sensorId}' not found");

        public static Error NoDataFound(string sensorId, DateTimeOffset start, DateTimeOffset end) => Error.NotFound(
            code: "Recording.NoDataFound",
            description: $"No recording data found for sensor '{sensorId}' between {start:O} and {end:O}");

        public static Error StorageError(Exception exception) => Error.Failure(
            code: "Recording.StorageError",
            description: $"Storage operation failed: {exception.Message}");
    }
}
