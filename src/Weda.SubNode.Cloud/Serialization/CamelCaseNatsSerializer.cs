using System.Buffers;
using System.Text.Json;


using NATS.Client.Core;


namespace Weda.SubNode.Cloud.Serialization;

/// <summary>
/// NATS serializer that uses camelCase for JSON property names.
/// Compatible with cloud services that use camelCase naming convention.
/// </summary>
public class CamelCaseNatsSerializer<T> : INatsSerialize<T>, INatsDeserialize<T>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Serialize(IBufferWriter<byte> bufferWriter, T value)
    {
        using var writer = new Utf8JsonWriter(bufferWriter);
        JsonSerializer.Serialize(writer, value, JsonOptions);
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length == 0)
            return default;

        var reader = new Utf8JsonReader(buffer);
        return JsonSerializer.Deserialize<T>(ref reader, JsonOptions);
    }
}

/// <summary>
/// Registry for camelCase NATS serializers.
/// Provides serializers that use Web defaults (camelCase, case-insensitive).
/// </summary>
public class CamelCaseNatsSerializerRegistry : INatsSerializerRegistry
{
    public static readonly CamelCaseNatsSerializerRegistry Default = new();

    public INatsSerialize<T> GetSerializer<T>()
    {
        return new CamelCaseNatsSerializer<T>();
    }

    public INatsDeserialize<T> GetDeserializer<T>()
    {
        return new CamelCaseNatsSerializer<T>();
    }
}
