using Weda.SubNode.Abstractions.Commands.Behaviors;

namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Defines the order of pipeline behaviors.
/// </summary>
public interface ICommandPipeline
{
    /// <summary>
    /// Gets the behavior types in execution order.
    /// When multiple attributes of the same behavior type exist,
    /// they are executed in attribute declaration order within their type's position.
    /// </summary>
    IReadOnlyList<Type> BehaviorOrder { get; }
}

/// <summary>
/// Default pipeline configuration.
/// Defines behavior execution order: Validation first, then Logging.
/// </summary>
/// <remarks>
/// Example:
/// <code>
/// [Validation(typeof(ValidatorA))]  // Position 1 in Validation phase
/// [Logging]                          // Position 1 in Logging phase
/// [Validation(typeof(ValidatorB))]  // Position 2 in Validation phase
/// </code>
/// Execution order: ValidatorA → ValidatorB → Logging
/// </remarks>
public class DefaultCommandPipeline : ICommandPipeline
{
    /// <summary>
    /// Gets the default behavior order.
    /// </summary>
    public virtual IReadOnlyList<Type> BehaviorOrder { get; } =
    [
        typeof(ValidatorBehavior<,>),
        typeof(LoggingBehavior<,>)
    ];
}
