using System.Text.Json;

namespace Weda.SubNode.Abstractions.Utilities;

/// <summary>
/// Utility class for safely extracting typed values from Dictionary&lt;string, object&gt;.
///
/// This is necessary because:
/// 1. Microsoft.Extensions.Configuration binds Dictionary&lt;string, object&gt; values as strings
/// 2. System.Text.Json deserializes object values as JsonElement
/// 3. Direct code assignments use native types (int, bool, etc.)
///
/// This converter handles all these cases uniformly.
/// </summary>
public static class DictionaryValueConverter
{
    /// <summary>
    /// Gets a string value from a dictionary.
    /// </summary>
    public static string GetString(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        string defaultValue = "")
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? defaultValue,
            JsonElement je => je.ToString(),
            _ => value.ToString() ?? defaultValue
        };
    }

    /// <summary>
    /// Gets an integer value from a dictionary.
    /// </summary>
    public static int GetInt32(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        int defaultValue = 0)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToInt32(value, defaultValue);
    }

    /// <summary>
    /// Gets a long value from a dictionary.
    /// </summary>
    public static long GetInt64(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        long defaultValue = 0)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToInt64(value, defaultValue);
    }

    /// <summary>
    /// Gets a double value from a dictionary.
    /// </summary>
    public static double GetDouble(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        double defaultValue = 0.0)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToDouble(value, defaultValue);
    }

    /// <summary>
    /// Gets a boolean value from a dictionary.
    /// </summary>
    public static bool GetBoolean(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        bool defaultValue = false)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToBoolean(value, defaultValue);
    }

    /// <summary>
    /// Gets an enum value from a dictionary.
    /// </summary>
    public static TEnum GetEnum<TEnum>(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        TEnum defaultValue = default) where TEnum : struct, Enum
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToEnum(value, defaultValue);
    }

    /// <summary>
    /// Gets an object value from a dictionary and deserializes it to the specified type.
    /// Handles JsonElement, Dictionary, and already-typed objects.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to</typeparam>
    /// <param name="dictionary">The source dictionary</param>
    /// <param name="key">The key to look up</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails</param>
    /// <param name="options">Optional JsonSerializerOptions for customizing deserialization</param>
    /// <returns>The deserialized object or default value</returns>
    public static T? GetObject<T>(
        this IReadOnlyDictionary<string, object> dictionary,
        string key,
        T? defaultValue = default,
        JsonSerializerOptions? options = null)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return ConvertToObject<T>(value, defaultValue, options);
    }

    /// <summary>
    /// Converts the entire dictionary to the specified type.
    /// Useful for converting DeviceConfiguration.Communication to a strongly-typed settings class.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to</typeparam>
    /// <param name="dictionary">The source dictionary to convert</param>
    /// <param name="defaultValue">Default value if conversion fails</param>
    /// <param name="options">Optional JsonSerializerOptions for customizing deserialization</param>
    /// <returns>The deserialized object or default value</returns>
    /// <example>
    /// var tcpSettings = configuration.Communication.GetObject&lt;TcpCommunicationSettings&gt;();
    /// </example>
    public static T? GetObject<T>(
        this IReadOnlyDictionary<string, object> dictionary,
        T? defaultValue = default,
        JsonSerializerOptions? options = null)
    {
        if (dictionary == null || dictionary.Count == 0)
            return defaultValue;

        // Debug: Log dictionary contents
        System.Diagnostics.Debug.WriteLine($"[DictionaryValueConverter] GetObject<{typeof(T).Name}> called with {dictionary.Count} items:");
        foreach (var kvp in dictionary)
        {
            System.Diagnostics.Debug.WriteLine($"  [{kvp.Key}] = {kvp.Value} (Type: {kvp.Value?.GetType().Name ?? "null"})");
        }

        return ConvertToObject<T>(dictionary, defaultValue, options);
    }

    /// <summary>
    /// Converts an object value to the specified type.
    /// Handles JsonElement, Dictionary, and already-typed objects.
    /// </summary>
    /// <typeparam name="T">The target type to convert to</typeparam>
    /// <param name="value">The value to convert</param>
    /// <param name="defaultValue">Default value if conversion fails</param>
    /// <param name="options">Optional JsonSerializerOptions for customizing deserialization</param>
    /// <returns>The converted object or default value</returns>
    public static T? ConvertToObject<T>(object? value, T? defaultValue = default, JsonSerializerOptions? options = null)
    {
        if (value == null)
            return defaultValue;

        // Already the correct type
        if (value is T typed)
            return typed;

        options ??= DefaultJsonOptions;

        try
        {
            // Handle JsonElement - deserialize directly
            if (value is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>(options);
            }

            // Handle IReadOnlyDictionary<string, object> - normalize values first then serialize
            if (value is IReadOnlyDictionary<string, object> readOnlyDict)
            {
                var normalizedDict = NormalizeDictionary(readOnlyDict);
                var json = JsonSerializer.Serialize(normalizedDict, options);
                return JsonSerializer.Deserialize<T>(json, options);
            }

            // Handle Dictionary<string, object> - normalize values first then serialize
            if (value is IDictionary<string, object> dict)
            {
                var normalizedDict = NormalizeDictionary(dict);
                var json = JsonSerializer.Serialize(normalizedDict, options);
                return JsonSerializer.Deserialize<T>(json, options);
            }

            // Handle other types - serialize then deserialize
            var serialized = JsonSerializer.Serialize(value, value.GetType(), options);
            return JsonSerializer.Deserialize<T>(serialized, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DictionaryValueConverter] ConvertToObject failed: {ex.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// Normalizes a dictionary by converting JsonElement values to their native types.
    /// This ensures proper serialization when the dictionary contains mixed types.
    /// </summary>
    private static Dictionary<string, object?> NormalizeDictionary(IEnumerable<KeyValuePair<string, object>> source)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = NormalizeValue(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Normalizes a value by converting JsonElement to its native type.
    /// Also handles string values that represent numbers or booleans
    /// (common when values come from Microsoft.Extensions.Configuration).
    /// </summary>
    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Undefined => null,
                JsonValueKind.Null => null,
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number when je.TryGetInt32(out var i) => i,
                JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                JsonValueKind.Number => je.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => je, // Keep as JsonElement for arrays
                JsonValueKind.Object => je, // Keep as JsonElement for nested objects
                _ => je.ToString()
            };
        }

        // Handle string values that might be numbers or booleans
        // (Microsoft.Extensions.Configuration binds all values as strings)
        if (value is string str)
        {
            // Try to parse as integer
            if (int.TryParse(str, out var intVal))
                return intVal;

            // Try to parse as long
            if (long.TryParse(str, out var longVal))
                return longVal;

            // Try to parse as double
            if (double.TryParse(str, out var doubleVal))
                return doubleVal;

            // Try to parse as boolean
            if (bool.TryParse(str, out var boolVal))
                return boolVal;

            // Return as string if no conversion needed
            return str;
        }

        return value;
    }

    /// <summary>
    /// Default JSON serializer options used for object conversion.
    /// </summary>
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Converts an object value to Int32.
    /// </summary>
    public static int ConvertToInt32(object? value, int defaultValue = 0)
    {
        return value switch
        {
            null => defaultValue,
            int i => i,
            long l => (int)l,
            double d => (int)d,
            float f => (int)f,
            decimal dec => (int)dec,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            JsonElement je when je.ValueKind == JsonValueKind.String =>
                int.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
            string s => int.TryParse(s, out var parsed) ? parsed : defaultValue,
            _ when IsNumericType(value) => Convert.ToInt32(value),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Converts an object value to Int64.
    /// </summary>
    public static long ConvertToInt64(object? value, long defaultValue = 0)
    {
        return value switch
        {
            null => defaultValue,
            long l => l,
            int i => i,
            double d => (long)d,
            float f => (long)f,
            decimal dec => (long)dec,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt64(),
            JsonElement je when je.ValueKind == JsonValueKind.String =>
                long.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
            string s => long.TryParse(s, out var parsed) ? parsed : defaultValue,
            _ when IsNumericType(value) => Convert.ToInt64(value),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Converts an object value to Double.
    /// </summary>
    public static double ConvertToDouble(object? value, double defaultValue = 0.0)
    {
        return value switch
        {
            null => defaultValue,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDouble(),
            JsonElement je when je.ValueKind == JsonValueKind.String =>
                double.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
            string s => double.TryParse(s, out var parsed) ? parsed : defaultValue,
            _ when IsNumericType(value) => Convert.ToDouble(value),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Converts an object value to Boolean.
    /// </summary>
    public static bool ConvertToBoolean(object? value, bool defaultValue = false)
    {
        return value switch
        {
            null => defaultValue,
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32() != 0,
            JsonElement je when je.ValueKind == JsonValueKind.String =>
                bool.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
            string s when bool.TryParse(s, out var parsed) => parsed,
            string s when int.TryParse(s, out var num) => num != 0,
            int i => i != 0,
            long l => l != 0,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Converts an object value to String.
    /// </summary>
    public static string ConvertToString(object? value, string defaultValue = "")
    {
        return value switch
        {
            null => defaultValue,
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? defaultValue,
            JsonElement je when je.ValueKind == JsonValueKind.Null => defaultValue,
            JsonElement je => je.ToString(),
            _ => value.ToString() ?? defaultValue
        };
    }

    /// <summary>
    /// Converts an object value to an Enum.
    /// </summary>
    public static TEnum ConvertToEnum<TEnum>(object? value, TEnum defaultValue = default)
        where TEnum : struct, Enum
    {
        if (value == null)
            return defaultValue;

        var stringValue = ConvertToString(value);

        if (string.IsNullOrEmpty(stringValue))
            return defaultValue;

        // Try parsing as name (case-insensitive)
        if (Enum.TryParse<TEnum>(stringValue, ignoreCase: true, out var result))
            return result;

        // Try parsing as number
        if (int.TryParse(stringValue, out var intValue) && Enum.IsDefined(typeof(TEnum), intValue))
            return (TEnum)(object)intValue;

        return defaultValue;
    }

    private static bool IsNumericType(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }
}
