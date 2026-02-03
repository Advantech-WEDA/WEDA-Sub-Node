using System.Collections.Concurrent;
using Receiver.Models;

namespace Receiver;

class ImageStore(int maxImages)
{
    private readonly ConcurrentDictionary<string, ChunkBuffer> _pending = new();
    private readonly LinkedList<ImageRecord> _completed = new();
    private readonly object _lock = new();

    public void AddComplete(string imageId, string base64, string sensorId)
    {
        var contentType = DetectContentType(base64);
        var record = new ImageRecord(imageId, base64, sensorId,
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), contentType);
        Store(record);
        Console.WriteLine($"[{record.Timestamp}] Image received: {sensorId}, {base64.Length} chars, {contentType}");
    }

    public void AddChunk(string imageId, int chunkIndex, int totalChunks, string chunk, string sensorId)
    {
        var buffer = _pending.GetOrAdd(imageId, _ => new ChunkBuffer(totalChunks, sensorId));
        buffer.Chunks[chunkIndex] = chunk;

        Console.WriteLine($"  Chunk {chunkIndex + 1}/{totalChunks} for {imageId[..8]}...");

        if (buffer.IsComplete)
        {
            _pending.TryRemove(imageId, out _);
            var fullBase64 = string.Concat(buffer.Chunks);
            var contentType = DetectContentType(fullBase64);
            var record = new ImageRecord(imageId, fullBase64, sensorId,
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), contentType);
            Store(record);
            Console.WriteLine($"[{record.Timestamp}] Image reassembled: {sensorId}, {totalChunks} chunks, {fullBase64.Length} chars, {contentType}");
        }
    }

    private void Store(ImageRecord record)
    {
        lock (_lock)
        {
            _completed.AddFirst(record);
            while (_completed.Count > maxImages)
                _completed.RemoveLast();
        }
    }

    public ImageRecord? GetLatest()
    {
        lock (_lock) { return _completed.First?.Value; }
    }

    public ImageRecord? Get(string imageId)
    {
        lock (_lock) { return _completed.FirstOrDefault(i => i.ImageId == imageId); }
    }

    public List<ImageRecord> GetAll()
    {
        lock (_lock) { return _completed.ToList(); }
    }

    private static string DetectContentType(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64.Length > 12 ? base64[..12] : base64);
            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";
        }
        catch { }
        return "application/octet-stream";
    }
}
