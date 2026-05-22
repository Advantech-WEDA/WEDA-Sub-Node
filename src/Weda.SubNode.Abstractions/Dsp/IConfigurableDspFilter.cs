using System.ComponentModel.DataAnnotations;

using ErrorOr;

namespace Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// Strongly-typed self-registering DSP filter interface backed by a parameter
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
/// public class MyFilterParameters
/// {
///     [Range(1, 100)] public int WindowSize { get; init; } = 5;
/// }
///
/// public class MyFilter : IDspFilter,
///     IConfigurableDspFilter&lt;MyFilter, MyFilterParameters&gt;
/// {
///     public static string TypeName => "myfilter";
///     public static string? Description => "Example DSP filter.";
///     public static MyFilter Create(MyFilterParameters parameters) => new(parameters);
///     public void UpdateParameters(MyFilterParameters parameters) { /* swap state */ }
/// }
/// </code>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself (CRTP pattern).</typeparam>
/// <typeparam name="TParameter">
/// Strongly-typed parameter POCO. Must be a reference type with a parameterless
/// constructor so the factory can deserialize JSON / Dictionary input into it.
/// </typeparam>
public interface IConfigurableDspFilter<TSelf, TParameter>
    where TSelf : IDspFilter, IConfigurableDspFilter<TSelf, TParameter>
    where TParameter : class, new()
{
    /// <summary>
    /// Stable identifier matched case-insensitively against <c>DspFilterConfig.Type</c>.
    /// </summary>
    static abstract string TypeName { get; }

    /// <summary>
    /// Human-readable description surfaced to cloud-side UI / agents through
    /// the capability descriptor. Return <c>null</c> when no description is needed.
    /// </summary>
    static abstract string? Description { get; }

    /// <summary>
    /// Creates an instance of the filter from validated parameters. The caller
    /// (DspFilterFactory) is expected to have already validated
    /// <paramref name="parameters"/> via <see cref="ValidateParameters"/>.
    /// </summary>
    static abstract TSelf Create(TParameter parameters);

    /// <summary>
    /// Updates filter parameters at runtime. State outside of
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
