namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Data type identifier for index records.
/// When schemaType >= 0x10, the MIME flag in IndexFlags MUST also be set.
/// </summary>
public enum SchemaType : byte
{
    // Primitives types (0x00 - 0x0F)
    Double = 0x00,
    Integer = 0x01,
    String = 0x02,
    Boolean = 0x03,

    // MIME types (0x10+)
    ImageJpeg = 0x10,
    ImagePng = 0x11,
    ApplicationJson = 0x20,
    ApplicationOctetStream = 0x21,
}