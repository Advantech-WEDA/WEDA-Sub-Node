namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// A record read from dynamic storage.
/// </summary>
public readonly record struct StorageRecord(
    long Timestamp,
    ReadOnlyMemory<byte> Payload,
    SchemaType SchemaType,
    IndexFlags Flags);