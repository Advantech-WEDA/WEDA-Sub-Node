using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Cloud.Serialization;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerOptions CamelCaseOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}