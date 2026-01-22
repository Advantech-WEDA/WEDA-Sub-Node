using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Configuration for a single behavior instance in the pipeline.
/// </summary>
public abstract record BehaviorConfiguration
{
    /// <summary>
    /// Gets the behavior type (e.g., ValidatorBehavior&lt;,&gt; or LoggingBehavior&lt;,&gt;).
    /// </summary>
    public abstract Type BehaviorType { get; }

    /// <summary>
    /// Gets the order index (based on attribute declaration order).
    /// </summary>
    public int Order { get; init; }
}

/// <summary>
/// Configuration for a ValidatorBehavior.
/// </summary>
public record ValidationBehaviorConfiguration : BehaviorConfiguration
{
    /// <summary>
    /// Gets the validator type that implements ICommandValidator&lt;TCommand&gt;.
    /// </summary>
    public required Type ValidatorType { get; init; }

    public override Type BehaviorType => typeof(Abstractions.Commands.Behaviors.ValidatorBehavior<,>);
}

/// <summary>
/// Configuration for a LoggingBehavior.
/// </summary>
public record LoggingBehaviorConfiguration : BehaviorConfiguration
{
    /// <summary>
    /// Gets the log level for command execution start.
    /// </summary>
    public LogLevel BeforeLevel { get; init; } = LogLevel.Debug;

    /// <summary>
    /// Gets the log level for successful command completion.
    /// </summary>
    public LogLevel AfterLevel { get; init; } = LogLevel.Debug;

    /// <summary>
    /// Gets the log level for command execution errors.
    /// </summary>
    public LogLevel ErrorLevel { get; init; } = LogLevel.Warning;

    public override Type BehaviorType => typeof(Abstractions.Commands.Behaviors.LoggingBehavior<,>);
}
