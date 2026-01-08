using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Weda.SubNode.Host.Configuration;

/// <summary>
/// Extensions for loading JSON configuration files into a specific configuration section.
/// This allows multiple JSON files to be loaded without key conflicts by prefixing all keys with a section name.
/// </summary>
public static class SectionedJsonConfigurationExtensions
{
    /// <summary>
    /// Adds a JSON configuration file to be loaded into a specific section.
    /// All keys from the JSON file will be prefixed with the section name.
    ///
    /// For example, if sectionName is "DeviceConfig" and the JSON contains:
    /// { "SubNode": { "Name": "Test" } }
    ///
    /// The configuration will be accessible as: Configuration["DeviceConfig:SubNode:Name"]
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="path">The path to the JSON file</param>
    /// <param name="sectionName">The section name to prefix all keys with</param>
    /// <param name="optional">Whether the file is optional</param>
    /// <param name="reloadOnChange">Whether to reload configuration when the file changes</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddJsonFileToSection(
        this IConfigurationBuilder builder,
        string path,
        string sectionName,
        bool optional = false,
        bool reloadOnChange = false)
    {
        return builder.Add(new SectionedJsonConfigurationSource
        {
            Path = path,
            SectionName = sectionName,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
            FileProvider = null,
            ReloadDelay = 250
        });
    }
}

/// <summary>
/// Configuration source that loads JSON files into a specific section.
/// </summary>
public class SectionedJsonConfigurationSource : JsonConfigurationSource
{
    /// <summary>
    /// The section name to prefix all keys with.
    /// </summary>
    public string SectionName { get; set; } = string.Empty;

    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new SectionedJsonConfigurationProvider(this);
    }
}

/// <summary>
/// Configuration provider that prefixes all keys with a section name.
/// </summary>
public class SectionedJsonConfigurationProvider : JsonConfigurationProvider
{
    private readonly string _sectionPrefix;

    public SectionedJsonConfigurationProvider(SectionedJsonConfigurationSource source)
        : base(source)
    {
        _sectionPrefix = string.IsNullOrEmpty(source.SectionName)
            ? string.Empty
            : source.SectionName + ConfigurationPath.KeyDelimiter;
    }

    /// <inheritdoc />
    public override void Load()
    {
        base.Load();

        if (string.IsNullOrEmpty(_sectionPrefix))
            return;

        // Prefix all keys with the section name
        var prefixedData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in Data)
        {
            prefixedData[_sectionPrefix + kvp.Key] = kvp.Value;
        }

        Data = prefixedData;
    }
}