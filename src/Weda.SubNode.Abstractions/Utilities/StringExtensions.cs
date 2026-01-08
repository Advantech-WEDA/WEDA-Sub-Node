namespace Weda.SubNode.Abstractions.Utilities;

/// <summary>
/// Extension methods for string operations.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Determines whether this string instance starts with the specified prefix.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="prefix">The prefix to compare.</param>
    /// <param name="comparison">The string comparison type. Default is OrdinalIgnoreCase.</param>
    /// <returns>true if the string starts with the prefix; otherwise, false.</returns>
    public static bool HasPrefix(this string? value, string prefix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.StartsWith(prefix, comparison);
    }

    /// <summary>
    /// Determines whether this string instance ends with the specified suffix.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="suffix">The suffix to compare.</param>
    /// <param name="comparison">The string comparison type. Default is OrdinalIgnoreCase.</param>
    /// <returns>true if the string ends with the suffix; otherwise, false.</returns>
    public static bool HasSuffix(this string? value, string suffix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.EndsWith(suffix, comparison);
    }

    /// <summary>
    /// Ensures that the string starts with the specified prefix.
    /// If the string already has the prefix, it is returned unchanged.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="prefix">The prefix to ensure.</param>
    /// <param name="comparison">The string comparison type. Default is OrdinalIgnoreCase.</param>
    /// <returns>The string with the prefix ensured.</returns>
    public static string EnsurePrefix(this string? value, string prefix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(value))
            return prefix;

        return value.HasPrefix(prefix, comparison) ? value : prefix + value;
    }

    /// <summary>
    /// Ensures that the string ends with the specified suffix.
    /// If the string already has the suffix, it is returned unchanged.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <param name="suffix">The suffix to ensure.</param>
    /// <param name="comparison">The string comparison type. Default is OrdinalIgnoreCase.</param>
    /// <returns>The string with the suffix ensured.</returns>
    public static string EnsureSuffix(this string? value, string suffix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(value))
            return suffix;

        return value.HasSuffix(suffix, comparison) ? value : value + suffix;
    }
}