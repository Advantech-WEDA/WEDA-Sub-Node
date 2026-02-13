namespace Receiver;

class ChunkBuffer(int totalChunks, string sensorId, uint? expectedChecksum = null)
{
    public string[] Chunks { get; } = new string[totalChunks];
    public string SensorId { get; } = sensorId;
    public uint? ExpectedChecksum { get; } = expectedChecksum;
    public bool IsComplete => Chunks.All(c => c != null);
}
