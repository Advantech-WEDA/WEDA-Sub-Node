namespace Weda.SubNode.Abstractions.Storage.Recordings;

public class RecordingFileHeader
{
    public const uint Prefix = 0x57454441; // "WEDA"
    public const int HeaderSize = 24;

    public ushort Version { get; set; } = 1;
    public ushort Interval { get; set; }
    public long StartTimestamp { get; set; }
    public int SlotCount { get; set; }
    public int CurrentSlot { get; set;}
}