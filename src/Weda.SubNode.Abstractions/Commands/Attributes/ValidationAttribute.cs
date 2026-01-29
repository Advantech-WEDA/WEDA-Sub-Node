namespace Weda.SubNode.Abstractions.Commands.Attributes;

/// <summary>
/// Specifies that the handler should use a custom validator in the pipeline.
/// Multiple ValidationAttributes can be applied, each adding a validator to the pipeline.
/// The order of attributes determines execution order within the validation phase.
/// </summary>
/// <example>
/// <code>
/// [Validation(typeof(TimeRangeValidator))]
/// [Validation(typeof(SensorFilterValidator))]
/// public class BatchReportCommandHandler : ICommandHandler&lt;BatchReportCommand, BatchReportResult&gt;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ValidationAttribute : Attribute
{
    /// <summary>
    /// Gets the validator type. Must implement <see cref="ICommandValidator{TCommand}"/>.
    /// </summary>
    public Type ValidatorType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationAttribute"/>.
    /// </summary>
    /// <param name="validatorType">The validator type. Must implement ICommandValidator&lt;TCommand&gt;.</param>
    public ValidationAttribute(Type validatorType)
    {
        ArgumentNullException.ThrowIfNull(validatorType);
        ValidatorType = validatorType;
    }
}
