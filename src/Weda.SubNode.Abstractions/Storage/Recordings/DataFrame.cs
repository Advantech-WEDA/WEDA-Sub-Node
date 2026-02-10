namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Data frame header for .dat file records.
/// Each record is prefixed with magic + size for recovery scanning.
/// </summary>
public static class DataFrame
{
    /// <summary>Frame start marker for recovery scanning.</summary>
    public const ushort Magic = 0xDA7A;
    
    /// <summary>Frame start marker for recovery scanning.</summary>
    public const int HeaderSize = 6;  // 2 (magic) + 4 (size)
}