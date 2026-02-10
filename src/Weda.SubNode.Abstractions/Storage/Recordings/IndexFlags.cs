namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Bitmask flags for index record metadata.
/// </summary>
[Flags]
public enum IndexFlags : byte
{
    None = 0b00,
    Mime = 0b01,
    Compressed = 0b10,
    Encrypted = 0b100,
    Deleted = 0b1000,
}