namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Application-level metadata supplied by the SubNode author via
/// <c>WedaApplicationBuilder.WithName(...)</c> / <c>.WithDescription(...)</c>.
/// Surfaces in the v1.2 upload payload as the SubNode wrapper Interface's
/// <c>displayName</c> / <c>description</c>. Falls back to <c>SubNodeInfo.Name</c>
/// and an auto-generated description when unset.
/// </summary>
public sealed record ProjectInfo(string? Name, string? Description)
{
    public static ProjectInfo Empty { get; } = new(null, null);
}
