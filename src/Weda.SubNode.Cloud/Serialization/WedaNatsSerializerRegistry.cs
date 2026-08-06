using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

using NATS.Client.Core;
using NATS.Client.Serializers.Json;


namespace Weda.SubNode.Cloud.Serialization;

/// <summary>
/// Default serializer registry that automatically selects the appropriate serializer based on type.
/// </summary>
public class WedaNatsSerializerRegistry : INatsSerializerRegistry
{
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Default instance of <see cref="WedaNatsSerializerRegistry"/> with default JSON options.
    /// </summary>
    public static readonly WedaNatsSerializerRegistry Default = new();

    /// <summary>
    /// The JSON serializer options used by the default registry instance.
    /// Exposed so outbound choke points (e.g. configuration upload/report) can
    /// serialize a payload with the exact same options the NATS JSON serializer
    /// would use, before running <see cref="CamelCaseJsonNormalizer"/> over the
    /// resulting bytes to force every key (including raw-echoed devicecfg keys and
    /// dictionary keys) to camelCase.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Creates a new instance of <see cref="WedaNatsSerializerRegistry"/> with default JSON options.
    /// </summary>
    public WedaNatsSerializerRegistry()
        : this(DefaultOptions)
    {
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="WedaNatsSerializerRegistry"/> with specified JSON options.
    /// </summary>
    /// <param name="options">JSON serialization options for object serialization</param>
    public WedaNatsSerializerRegistry(JsonSerializerOptions options)
    {
        _jsonOptions = options;
    }

    /// <inheritdoc />
    public INatsDeserialize<T> GetDeserializer<T>()
    {
        var type = typeof(T);
        
        // Check for raw byte types
        if (IsRawByteType(type))
        {
            return NatsRawSerializer<T>.Default;
        }
        
        // Check for primitive types
        if (IsPrimitiveType(type))
        {
            return NatsUtf8PrimitivesSerializer<T>.Default;
        }
        
        // For all other types (objects), use JSON serializer
        return new NatsJsonSerializer<T>(_jsonOptions);
    }


    /// <inheritdoc />
    public INatsSerialize<T> GetSerializer<T>()
    {
               var type = typeof(T);
        
        // Check for raw byte types
        if (IsRawByteType(type))
        {
            return NatsRawSerializer<T>.Default;
        }
        
        // Check for primitive types
        if (IsPrimitiveType(type))
        {
            return NatsUtf8PrimitivesSerializer<T>.Default;
        }
        
        // For all other types (objects), use JSON serializer
        return new NatsJsonSerializer<T>(_jsonOptions);
    }


    private static bool IsRawByteType(Type type)
    {
        return type == typeof(byte[]) ||
               type == typeof(Memory<byte>) ||
               type == typeof(ReadOnlyMemory<byte>) ||
               type == typeof(ReadOnlySequence<byte>) ||
               type == typeof(IMemoryOwner<byte>) ||
               type == typeof(NatsMemoryOwner<byte>);
    }
    
    private static bool IsPrimitiveType(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        return underlyingType == typeof(string) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(DateTimeOffset) ||
               underlyingType == typeof(Guid) ||
               underlyingType == typeof(TimeSpan) ||
               underlyingType == typeof(bool) ||
               underlyingType == typeof(byte) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(float) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(sbyte) ||
               underlyingType == typeof(short) ||
               underlyingType == typeof(uint) ||
               underlyingType == typeof(ulong);
    }
}