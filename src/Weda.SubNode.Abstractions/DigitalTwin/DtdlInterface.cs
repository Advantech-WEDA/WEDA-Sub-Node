using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.DigitalTwin;

/// <summary>
/// Represents a DTDL (Digital Twin Definition Language) Interface.
/// Based on DTDL v2 specification.
/// </summary>
public class DtdlInterface
{
    /// <summary>
    /// The identifier for the interface (e.g., "dtmi:advantech:EdgeSync:DeviceSensors;1").
    /// </summary>
    [JsonPropertyName("@id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The type of this DTDL element (should be "Interface").
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Interface";

    /// <summary>
    /// Optional context for the interface.
    /// </summary>
    [JsonPropertyName("@context")]
    public object? Context { get; set; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional description of the interface.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// The contents of the interface (telemetry, properties, commands).
    /// </summary>
    [JsonPropertyName("contents")]
    public List<DtdlContent> Contents { get; set; } = new();

    /// <summary>
    /// Optional extends clause to inherit from other interfaces.
    /// </summary>
    [JsonPropertyName("extends")]
    public object? Extends { get; set; }

    /// <summary>
    /// Optional schemas defined in this interface.
    /// </summary>
    [JsonPropertyName("schemas")]
    public List<object>? Schemas { get; set; }

    /// <summary>
    /// Loads a DTDL interface from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing the DTDL interface definition.</param>
    /// <returns>A DtdlInterface object deserialized from the JSON file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
    public static DtdlInterface Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"DTDL file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DtdlInterface>(json)
            ?? throw new JsonException($"Failed to deserialize DTDL interface from file: {filePath}");
    }

    /// <summary>
    /// Asynchronously loads a DTDL interface from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing the DTDL interface definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A DtdlInterface object deserialized from the JSON file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
    public static async Task<DtdlInterface> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"DTDL file not found: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<DtdlInterface>(stream, cancellationToken: cancellationToken)
            ?? throw new JsonException($"Failed to deserialize DTDL interface from file: {filePath}");
    }
}

/// <summary>
/// Represents content within a DTDL Interface (Telemetry, Property, Command, etc.).
/// </summary>
public class DtdlContent
{
    /// <summary>
    /// The identifier for this content.
    /// </summary>
    [JsonPropertyName("@id")]
    public string? Id { get; set; }

    /// <summary>
    /// The type of this content (e.g., "Telemetry", "Property", "Command").
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The name of this content.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// The schema for this content (e.g., "double", "string", complex schema).
    /// </summary>
    [JsonPropertyName("schema")]
    public object? Schema { get; set; }

    /// <summary>
    /// For properties: whether it's writable.
    /// </summary>
    [JsonPropertyName("writable")]
    public bool? Writable { get; set; }

    /// <summary>
    /// Optional unit for telemetry/property values.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// For commands: the request schema.
    /// </summary>
    [JsonPropertyName("request")]
    public object? Request { get; set; }

    /// <summary>
    /// For commands: the response schema.
    /// </summary>
    [JsonPropertyName("response")]
    public object? Response { get; set; }
}
