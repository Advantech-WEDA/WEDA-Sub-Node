using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Schema;

/// <summary>
/// Bidirectional bridge between <c>Dictionary&lt;string, object&gt;</c> (the on-disk /
/// over-the-wire form used throughout SubNode configuration) and a strongly-typed
/// POCO with DataAnnotation validation.
/// </summary>
/// <remarks>
/// Conversion uses a JSON round-trip rather than ad-hoc reflection so that the
/// canonical wire format (<see cref="JsonPropertyNameAttribute"/>,
/// <see cref="JsonStringEnumConverter"/>, case-insensitive matching) drives both
/// directions. Validation runs after deserialisation via
/// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>,
/// honouring DataAnnotations and <see cref="IValidatableObject"/>.
///
/// <para>The same algorithm is already proven in <c>TransformFactory.TryRegister</c>
/// — this class extracts it for reuse across transform / DSP filter / command /
/// device / sensor factories.</para>
/// </remarks>
public static class TypedParameterConverter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T FromDictionary<T>(IReadOnlyDictionary<string, object>? source)
        where T : class, new()
    {
        var json = JsonSerializer.Serialize(
            source ?? (IReadOnlyDictionary<string, object>)new Dictionary<string, object>(),
            JsonOpts);

        var typed = JsonSerializer.Deserialize<T>(json, JsonOpts)
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize a {typeof(T).Name} from the input dictionary.");

        var ctx = new ValidationContext(typed);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(typed, ctx, results, validateAllProperties: true))
        {
            var detail = string.Join("; ",
                results.Select(r => r.ErrorMessage ?? "validation failed"));
            throw new ValidationException($"{typeof(T).Name}: {detail}");
        }
        return typed;
    }

    public static Dictionary<string, object> ToDictionary<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, JsonOpts);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOpts)
               ?? new Dictionary<string, object>();
    }
}
