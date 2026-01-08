namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Subscription type identifier. Uses string for extensibility.
/// New subscription types can be added dynamically without code changes.
/// </summary>
public sealed partial class SubscriptionType : IEquatable<SubscriptionType>
{
    /// <summary>
    /// The type identifier.
    /// </summary>
    public string Value { get; }

    private SubscriptionType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Creates a new subscription type.
    /// </summary>
    public static SubscriptionType Create(string value) => new(value);

    // ===== Equality =====

    public bool Equals(SubscriptionType? other) =>
        other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as SubscriptionType);

    public override int GetHashCode() => Value.ToLowerInvariant().GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(SubscriptionType? left, SubscriptionType? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(SubscriptionType? left, SubscriptionType? right) =>
        !(left == right);

    public static implicit operator string(SubscriptionType type) => type.Value;
}
