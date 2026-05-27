using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.Dtdl.Emit;
using Weda.Dtdl.Validation;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Behaviors;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Schema;

namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Registry for command handlers. Supports both manual registration and automatic assembly scanning.
/// Pipeline behaviors are configured via attributes on handler classes.
/// </summary>
/// <remarks>
/// Pipeline configuration:
/// - DataAnnotation validation always runs (outside pipeline)
/// - [Validation(typeof(...))] adds ValidatorBehavior to pipeline
/// - [Logging] adds LoggingBehavior to pipeline
/// - DefaultCommandPipeline defines behavior type ordering
/// - Same-type behaviors execute in attribute declaration order
/// </remarks>
public class CommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;
    private ICommandPipeline _defaultPipeline = new DefaultCommandPipeline();

    /// <summary>
    /// Initializes a new instance of <see cref="CommandRegistry"/>.
    /// </summary>
    public CommandRegistry(ILogger<CommandRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the default pipeline configuration.
    /// </summary>
    public ICommandPipeline DefaultPipeline
    {
        get => _defaultPipeline;
        set => _defaultPipeline = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the registered command names (for diagnostics/debugging).
    /// </summary>
    public IReadOnlyCollection<string> RegisteredCommands => _registrations.Keys;

    /// <summary>
    /// Scans an assembly for all types that implement <see cref="ICommandHandler{TCommand, TResult}"/>
    /// and automatically registers them based on the <see cref="DeviceCmdAttribute"/> on the command type.
    /// Also scans handler attributes to configure pipeline behaviors.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    public void ScanAssembly(Assembly assembly)
    {
        var handlerInterfaceType = typeof(ICommandHandler<,>);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            // Find ICommandHandler<TCommand, TResult> interface
            var handlerInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == handlerInterfaceType);

            if (handlerInterface is null)
                continue;

            var commandType = handlerInterface.GetGenericArguments()[0];
            var resultType = handlerInterface.GetGenericArguments()[1];

            // Get command name from [DeviceCmd] attribute
            var commandName = GetCommandName(commandType);
            if (string.IsNullOrEmpty(commandName))
            {
                _logger?.LogWarning(
                    "Handler {HandlerType} skipped: Command type {CommandType} is missing [DeviceCmd] attribute",
                    type.Name, commandType.Name);
                continue;
            }

            // Skip if already registered
            if (_registrations.ContainsKey(commandName))
            {
                _logger?.LogWarning(
                    "Handler {HandlerType} skipped: Command '{CommandName}' is already registered",
                    type.Name, commandName);
                continue;
            }

            // Scan handler attributes to build behavior configurations
            var behaviorConfigs = ScanHandlerAttributes(type);
            var autoAckEnabled = IsAutoAckEnabled(type);

            var parameterType = GetCommandParameterType(commandType);
            var description = commandType.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var parameterSchema = EmitCommandInterface(
                commandName, "param", parameterType ?? typeof(EmptyParameters), description);
            var responseSchema = EmitCommandInterface(
                commandName, "response", resultType, description: null);

            // Build the Step-0 validator eagerly so any DTDL schema bug (or
            // ConfigConstraint extension misuse) fails fast at startup rather
            // than at first dispatch.
            var parameterValidator = new WedaDtdlValidator(parameterSchema.ToJsonString());

            _registrations[commandName] = new CommandRegistration(
                CommandName: commandName,
                CommandType: commandType,
                ResultType: resultType,
                HandlerType: type,
                BehaviorConfigurations: behaviorConfigs,
                AutoAckEnabled: autoAckEnabled,
                ParameterType: parameterType,
                Description: description,
                ParameterSchema: parameterSchema,
                ResponseSchema: responseSchema,
                ParameterValidator: parameterValidator);

            _logger?.LogDebug(
                "Registered command handler: {CommandName} -> {HandlerType} (Behaviors: {BehaviorCount})",
                commandName, type.Name, behaviorConfigs.Count);
        }
    }

    /// <summary>
    /// Returns the descriptor list for every registered command. Used by
    /// <c>DeviceConfigurationMappingExtensions</c> when uploading SubNode
    /// capabilities to cloud.
    /// </summary>
    public IReadOnlyList<CommandDescriptorDto> GetDescriptors() =>
        _registrations.Values
            .Select(r => new CommandDescriptorDto(
                Name: r.CommandName,
                Description: r.Description,
                ParameterSchema: r.ParameterSchema
                    ?? EmitCommandInterface(r.CommandName, "param", typeof(EmptyParameters), description: null),
                ResponseSchema: r.ResponseSchema
                    ?? EmitCommandInterface(r.CommandName, "response", typeof(EmptyParameters), description: null),
                AutoAck: r.AutoAckEnabled))
            .ToList();

    /// <summary>
    /// Emits a DTDL v3 Interface that describes either the parameter shape or the
    /// response shape of a command. Each command yields TWO interfaces with DTMIs
    /// <c>dtmi:advantech:weda:command:&lt;cmd&gt;:param;1</c> and
    /// <c>dtmi:advantech:weda:command:&lt;cmd&gt;:response;1</c> — both bound to a
    /// single Property named after the role (<c>parameters</c> / <c>response</c>).
    /// </summary>
    private static JsonObject EmitCommandInterface(
        string commandName, string role, Type pocoType, string? description)
    {
        var propertyName = role == "response" ? "response" : "parameters";
        return DtdlInterfaceEmitter.Emit(
            new DtdlInterfaceEmitter.Options(
                Prefix: "dtmi:advantech:weda:command",
                Category: commandName,
                TypeName: role,
                DisplayName: $"{commandName} {role}",
                Description: description),
            new DtdlInterfaceEmitter.PropertyBinding(
                Name: propertyName,
                Type: pocoType));
    }

    /// <summary>
    /// Resolves the TParameter generic argument from <see cref="ICommand{TParameter}"/>
    /// on the command type. Returns null if the command does not implement the generic
    /// variant (rare; falls back to empty parameter schema).
    /// </summary>
    private static Type? GetCommandParameterType(Type commandType)
    {
        var iface = commandType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        return iface?.GetGenericArguments()[0];
    }

    /// <summary>
    /// Scans handler attributes to build behavior configurations.
    /// </summary>
    private List<BehaviorConfiguration> ScanHandlerAttributes(Type handlerType)
    {
        var configs = new List<BehaviorConfiguration>();
        var attributes = handlerType.GetCustomAttributes(inherit: false);
        var order = 0;

        foreach (var attr in attributes)
        {
            switch (attr)
            {
                case ValidationAttribute validationAttr:
                    configs.Add(new ValidationBehaviorConfiguration
                    {
                        ValidatorType = validationAttr.ValidatorType,
                        Order = order++
                    });
                    break;

                case LoggingAttribute loggingAttr:
                    configs.Add(new LoggingBehaviorConfiguration
                    {
                        BeforeLevel = loggingAttr.BeforeLevel,
                        AfterLevel = loggingAttr.AfterLevel,
                        ErrorLevel = loggingAttr.ErrorLevel,
                        Order = order++
                    });
                    break;
            }
        }

        return configs;
    }

    /// <summary>
    /// Checks if auto ack is enabled for the handler type.
    /// </summary>
    private static bool IsAutoAckEnabled(Type handlerType)
    {
        var autoAckAttr = handlerType.GetCustomAttribute<AutoAckAttribute>();
        return autoAckAttr?.Enabled ?? true; // Default is enabled
    }

    /// <summary>
    /// Manually registers a command handler.
    /// </summary>
    public void Register<TCommand, TResult>(
        string commandName,
        ICommandHandler<TCommand, TResult> handler,
        List<BehaviorConfiguration>? behaviorConfigs = null)
        where TCommand : ICommand
    {
        if (_registrations.ContainsKey(commandName))
        {
            throw new InvalidOperationException($"Command '{commandName}' is already registered");
        }

        _registrations[commandName] = new CommandRegistration(
            CommandName: commandName,
            CommandType: typeof(TCommand),
            ResultType: typeof(TResult),
            HandlerType: handler.GetType(),
            HandlerInstance: handler,
            BehaviorConfigurations: behaviorConfigs ?? []);

        _logger?.LogDebug(
            "Registered command handler: {CommandName} -> {HandlerType}",
            commandName, handler.GetType().Name);
    }

    /// <summary>
    /// Gets the registration for the specified command name.
    /// </summary>
    /// <param name="commandName">The command name to look up.</param>
    /// <returns>The registration, or null if not found.</returns>
    public CommandRegistration? GetRegistration(string commandName)
    {
        return _registrations.TryGetValue(commandName, out var registration) ? registration : null;
    }

    /// <summary>
    /// Creates a handler delegate for the given registration.
    /// The delegate receives an already-deserialized command object.
    /// </summary>
    /// <param name="registration">The command registration.</param>
    /// <returns>A handler delegate, or null if creation failed.</returns>
    public Func<object, IWedaApplicationContext, CancellationToken, Task<ErrorOr<object?>>>? CreateHandler(
        CommandRegistration registration)
    {
        // Get or create handler instance
        var handlerInstance = registration.HandlerInstance;
        if (handlerInstance is null)
        {
            try
            {
                handlerInstance = Activator.CreateInstance(registration.HandlerType);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Failed to create handler instance for {HandlerType}",
                    registration.HandlerType.Name);
                return null;
            }
        }

        if (handlerInstance is null)
        {
            _logger?.LogWarning(
                "Handler instance is null for {HandlerType}",
                registration.HandlerType.Name);
            return null;
        }

        return async (command, context, ct) =>
        {
            // Use reflection to call HandleAsync
            var method = registration.HandlerType.GetMethod("HandleAsync");
            if (method is null)
            {
                return Errors.Command.ExecutionFailed($"Handler {registration.HandlerType.Name} does not have HandleAsync method");
            }

            var task = (Task)method.Invoke(handlerInstance, [command, context, ct])!;
            await task.ConfigureAwait(false);

            // Get the Result property from Task<ErrorOr<TResult>>
            var resultProperty = task.GetType().GetProperty("Result");
            var result = resultProperty?.GetValue(task);

            if (result is null)
            {
                return Errors.Command.ExecutionFailed("Handler returned null result");
            }

            // Convert ErrorOr<TResult> to ErrorOr<object?>
            return ConvertToObjectResult(result);
        };
    }

    /// <summary>
    /// Creates behavior instances for the given registration based on attribute configurations.
    /// Behaviors are sorted by DefaultPipeline order, then by attribute declaration order.
    /// </summary>
    /// <param name="registration">The command registration.</param>
    /// <returns>List of behavior instances in execution order.</returns>
    public IReadOnlyList<object> CreateBehaviors(CommandRegistration registration)
    {
        var behaviors = new List<object>();
        var pipelineOrder = _defaultPipeline.BehaviorOrder;

        // Sort configurations by pipeline order, then by declaration order
        var sortedConfigs = registration.BehaviorConfigurations
            .OrderBy(c => GetBehaviorTypeIndex(c.BehaviorType, pipelineOrder))
            .ThenBy(c => c.Order)
            .ToList();

        foreach (var config in sortedConfigs)
        {
            try
            {
                var instance = CreateBehaviorInstance(config, registration);
                if (instance is not null)
                {
                    behaviors.Add(instance);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Failed to create behavior instance for {BehaviorType}",
                    config.BehaviorType.Name);
            }
        }

        return behaviors;
    }

    /// <summary>
    /// Gets the index of a behavior type in the pipeline order.
    /// </summary>
    private static int GetBehaviorTypeIndex(Type behaviorType, IReadOnlyList<Type> pipelineOrder)
    {
        for (int i = 0; i < pipelineOrder.Count; i++)
        {
            var orderType = pipelineOrder[i];

            // Handle open generic types like ValidatorBehavior<,>
            if (orderType.IsGenericTypeDefinition && behaviorType.IsGenericTypeDefinition)
            {
                if (orderType == behaviorType)
                    return i;
            }
            else if (orderType.IsGenericTypeDefinition)
            {
                // Check if behaviorType is a closed version of orderType
                if (behaviorType.IsGenericType &&
                    behaviorType.GetGenericTypeDefinition() == orderType)
                    return i;
            }
            else if (orderType == behaviorType)
            {
                return i;
            }
        }

        // Not found in pipeline order, put at end
        return int.MaxValue;
    }

    /// <summary>
    /// Creates a behavior instance from its configuration.
    /// </summary>
    private object? CreateBehaviorInstance(BehaviorConfiguration config, CommandRegistration registration)
    {
        switch (config)
        {
            case ValidationBehaviorConfiguration validationConfig:
                return CreateValidatorBehavior(validationConfig, registration);

            case LoggingBehaviorConfiguration loggingConfig:
                return CreateLoggingBehavior(loggingConfig, registration);

            default:
                _logger?.LogWarning("Unknown behavior configuration type: {ConfigType}", config.GetType().Name);
                return null;
        }
    }

    /// <summary>
    /// Creates a ValidatorBehavior instance with the specified validator.
    /// </summary>
    private object? CreateValidatorBehavior(ValidationBehaviorConfiguration config, CommandRegistration registration)
    {
        // Create the validator instance
        var validatorInstance = Activator.CreateInstance(config.ValidatorType);
        if (validatorInstance is null)
        {
            _logger?.LogWarning("Failed to create validator instance for {ValidatorType}", config.ValidatorType.Name);
            return null;
        }

        // Create ValidatorBehavior<TCommand, TResult> with the validator
        var behaviorType = typeof(ValidatorBehavior<,>).MakeGenericType(
            registration.CommandType,
            registration.ResultType);

        // Find constructor that takes ICommandValidator<TCommand>
        var validatorInterfaceType = typeof(ICommandValidator<>).MakeGenericType(registration.CommandType);
        var constructor = behaviorType.GetConstructor([validatorInterfaceType]);

        if (constructor is not null)
        {
            return constructor.Invoke([validatorInstance]);
        }

        // Fallback: try parameterless constructor
        return Activator.CreateInstance(behaviorType);
    }

    /// <summary>
    /// Creates a LoggingBehavior instance with the specified log levels.
    /// </summary>
    private object? CreateLoggingBehavior(LoggingBehaviorConfiguration config, CommandRegistration registration)
    {
        var behaviorType = typeof(LoggingBehavior<,>).MakeGenericType(
            registration.CommandType,
            registration.ResultType);

        // Find constructor that takes log levels
        var constructor = behaviorType.GetConstructor([typeof(LogLevel), typeof(LogLevel), typeof(LogLevel)]);

        if (constructor is not null)
        {
            return constructor.Invoke([config.BeforeLevel, config.AfterLevel, config.ErrorLevel]);
        }

        // Fallback: parameterless constructor with default levels
        return Activator.CreateInstance(behaviorType);
    }

    /// <summary>
    /// Deserializes the command data to the specified command type.
    /// </summary>
    /// <param name="data">The raw JSON data from CommandMessage.</param>
    /// <param name="commandType">The target command type.</param>
    /// <returns>The deserialized command object.</returns>
    /// <exception cref="InvalidOperationException">If deserialization fails.</exception>
    public object DeserializeCommand(JsonElement? data, Type commandType)
    {
        if (data is null || data.Value.ValueKind == JsonValueKind.Null || data.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Command data is null");
        }

        return data.Value.Deserialize(commandType, JsonSerializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize command to {commandType.Name}");
    }

    /// <summary>
    /// Gets the command name from the <see cref="DeviceCmdAttribute"/> on the command type.
    /// </summary>
    private static string? GetCommandName(Type commandType)
    {
        var attr = commandType.GetCustomAttribute<DeviceCmdAttribute>();
        return attr?.Name;
    }

    /// <summary>
    /// Converts ErrorOr&lt;TResult&gt; to ErrorOr&lt;object?&gt;.
    /// </summary>
    internal static ErrorOr<object?> ConvertToObjectResult(object errorOrResult)
    {
        var type = errorOrResult.GetType();

        // Check if it's an error
        var isErrorProperty = type.GetProperty("IsError");
        var isError = (bool)(isErrorProperty?.GetValue(errorOrResult) ?? false);

        if (isError)
        {
            var errorsProperty = type.GetProperty("Errors");
            var errors = errorsProperty?.GetValue(errorOrResult) as List<Error>;
            return errors ?? [Errors.Command.ExecutionFailed("Unknown error")];
        }

        // Get the value
        var valueProperty = type.GetProperty("Value");
        var value = valueProperty?.GetValue(errorOrResult);
        return value;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Holds registration information for a command handler.
/// </summary>
public record CommandRegistration(
    string CommandName,
    Type CommandType,
    Type ResultType,
    Type HandlerType,
    object? HandlerInstance = null,
    IReadOnlyList<BehaviorConfiguration>? BehaviorConfigurations = null,
    bool AutoAckEnabled = true,
    Type? ParameterType = null,
    string? Description = null,
    JsonObject? ParameterSchema = null,
    JsonObject? ResponseSchema = null,
    WedaDtdlValidator? ParameterValidator = null)
{
    /// <summary>
    /// Gets the behavior configurations, never null.
    /// </summary>
    public IReadOnlyList<BehaviorConfiguration> BehaviorConfigurations { get; init; } =
        BehaviorConfigurations ?? [];
}
