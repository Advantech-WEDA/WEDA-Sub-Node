namespace Weda.SubNode.Abstractions.Transforms;

using System.ComponentModel.DataAnnotations;

using ErrorOr;

/// <summary>
/// Strongly-typed self-registering transform interface backed by a parameter
/// POCO class. Schemas and validation are derived from DataAnnotations applied
/// to <typeparamref name="TParameter"/>.
/// </summary>
/// <remarks>
/// Implementations supply a static factory plus a runtime parameter update
/// hook. <see cref="ValidateParameters(TParameter)"/> has a default
/// implementation that runs <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/> over the
/// annotations on <typeparamref name="TParameter"/>; override only when
/// cross-field semantics are required (or implement
/// <see cref="IValidatableObject"/> on the parameter type instead).
/// <para>
/// Example:
/// </para>
/// <code>
/// public class MyParameters
/// {
///     [Required] public string Mode { get; init; } = "";
///     [Range(0, 100)] public int Threshold { get; init; }
/// }
///
/// public class MyTransform : ITelemetryTransform,
///     IConfigurableTransform&lt;MyTransform, MyParameters&gt;
/// {
///     public static string TypeName => "mytransform";
///     public static string? Description => "Example transform.";
///     public static MyTransform Create(MyParameters parameters) => new(parameters);
///     public void UpdateParameters(MyParameters parameters) { /* swap state */ }
/// }
/// </code>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself (CRTP pattern).</typeparam>
/// <typeparam name="TParameter">
/// Strongly-typed parameter POCO. Must be a reference type with a parameterless
/// constructor so the factory can deserialize JSON / Dictionary input into it.
/// </typeparam>
public interface IConfigurableTransform<TSelf, TParameter>
    where TSelf : ITelemetryTransform, IConfigurableTransform<TSelf, TParameter>
    where TParameter : class, new()
{
    /// <summary>
    /// Stable identifier matched case-insensitively against <c>TransformConfig.Type</c>.
    /// </summary>
    static abstract string TypeName { get; }

    /// <summary>
    /// Human-readable description surfaced to cloud-side UI / agents through
    /// the capability descriptor. Return <c>null</c> when no description is needed.
    /// </summary>
    static abstract string? Description { get; }

    /// <summary>
    /// Creates an instance of the transform from validated parameters. The
    /// caller (TransformFactory) is expected to have already validated
    /// <paramref name="parameters"/> via <see cref="ValidateParameters"/>.
    /// </summary>
    static abstract TSelf Create(TParameter parameters);

    /// <summary>
    /// Updates transform parameters at runtime. State outside of
    /// <paramref name="parameters"/> is preserved. Callers must invoke
    /// <see cref="ValidateParameters"/> first.
    /// </summary>
    void UpdateParameters(TParameter parameters);

    /// <summary>
    /// Validates <paramref name="parameters"/>. The default implementation runs
    /// DataAnnotations + <see cref="IValidatableObject"/> via
    /// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>. Override to add bespoke
    /// validation logic.
    /// </summary>
    ErrorOr<Success> ValidateParameters(TParameter parameters)
    {
        var context = new ValidationContext(parameters);
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(parameters, context, results, validateAllProperties: true))
        {
            return Result.Success;
        }

        return results
            .Select(r => Error.Validation(
                code: $"{typeof(TParameter).Name}.{r.MemberNames.FirstOrDefault() ?? "?"}",
                description: r.ErrorMessage ?? "Validation failed"))
            .ToList();
    }
}
