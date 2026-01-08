using System.Text.Json;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Custom configuration loaded from customcfg.json.
/// Contains application-specific settings as a flexible dictionary.
/// Can be configured programmatically or loaded from configuration files.
/// </summary>
/// <example>
/// Programmatic configuration:
/// <code>
/// var customCfg = new CustomCfg
/// {
///     ["MyCustomSetting"] = "value1",
///     ["FeatureFlags"] = new Dictionary&lt;string, bool&gt;
///     {
///         ["EnableFeatureX"] = true,
///         ["EnableFeatureY"] = false
///     }
/// };
/// </code>
///
/// customcfg.json:
/// <code>
/// {
///   "MyCustomSetting": "value1",
///   "FeatureFlags": {
///     "EnableFeatureX": true,
///     "EnableFeatureY": false
///   },
///   "Thresholds": {
///     "MaxRetries": 3,
///     "TimeoutSeconds": 30
///   }
/// }
/// </code>
/// </example>
public class CustomCfg : Dictionary<string, object?>
{
    /// <summary>
    /// The configuration section name for the entire custom configuration.
    /// </summary>
    public const string SectionName = "CustomConfig";

    /// <summary>
    /// Creates an empty CustomCfg.
    /// </summary>
    public CustomCfg() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Creates a CustomCfg from an existing dictionary.
    /// </summary>
    public CustomCfg(IDictionary<string, object?> dictionary)
        : base(dictionary, StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Gets a typed value from the configuration.
    /// </summary>
    /// <typeparam name="T">The expected type</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">Default value if key not found or type mismatch</param>
    /// <returns>The typed value or default</returns>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (!TryGetValue(key, out var value) || value == null)
            return defaultValue;

        if (value is T typedValue)
            return typedValue;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        // Try conversion
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a nested configuration section as a typed object.
    /// </summary>
    /// <typeparam name="T">The expected type</typeparam>
    /// <param name="key">The configuration key</param>
    /// <returns>The typed object or null</returns>
    public T? GetSection<T>(string key) where T : class
    {
        if (!TryGetValue(key, out var value) || value == null)
            return null;

        if (value is T typedValue)
            return typedValue;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
