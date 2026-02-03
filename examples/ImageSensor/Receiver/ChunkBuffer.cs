namespace Receiver;

class ChunkBuffer(int totalChunks, string sensorId)
{
    public string[] Chunks { get; } = new string[totalChunks];
    public string SensorId { get; } = sensorId;
    public bool IsComplete => Chunks.All(c => c != null);
}
