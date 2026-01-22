using System.Reflection;
using System.Text.Json;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Registry for command handlers. Supports both manual registration and automatic assembly scanning.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CommandRegistry"/>.
    /// </summary>
    public CommandRegistry(ILogger<CommandRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the registered command names (for diagnostics/debugging).
    /// </summary>
    public IReadOnlyCollection<string> RegisteredCommands => _registrations.Keys;

    /// <summary>
    /// Scans an assembly for all types that implement <see cref="ICommandHandler{TCommand, TResult}"/>
    /// and automatically registers them based on the <see cref="DeviceCmdAttribute"/> on the command type.
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

            _registrations[commandName] = new CommandRegistration(
                CommandName: commandName,
                CommandType: commandType,
                ResultType: resultType,
                HandlerType: type);

            _logger?.LogDebug(
                "Registered command handler: {CommandName} -> {HandlerType}",
                commandName, type.Name);
        }
    }

    /// <summary>
    /// Manually registers a command handler.
    /// </summary>
    public void Register<TCommand, TResult>(
        string commandName,
        ICommandHandler<TCommand, TResult> handler)
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
            HandlerInstance: handler);

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
    /// Deserializes the command envelope data to the specified command type.
    /// </summary>
    /// <param name="envelope">The command envelope containing raw data.</param>
    /// <param name="commandType">The target command type.</param>
    /// <returns>The deserialized command object.</returns>
    /// <exception cref="InvalidOperationException">If deserialization fails.</exception>
    public object DeserializeCommand(CommandEnvelope envelope, Type commandType)
    {
        if (envelope.Data is null)
        {
            throw new InvalidOperationException("Command data is null");
        }

        // If data is already the correct type, return it directly
        if (envelope.Data.GetType() == commandType)
        {
            return envelope.Data;
        }

        // If data is a JsonElement, deserialize it
        if (envelope.Data is JsonElement jsonElement)
        {
            return jsonElement.Deserialize(commandType, JsonSerializerOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize command to {commandType.Name}");
        }

        // Otherwise, serialize then deserialize (handles Dictionary<string, object> etc.)
        var json = JsonSerializer.Serialize(envelope.Data, JsonSerializerOptions);
        return JsonSerializer.Deserialize(json, commandType, JsonSerializerOptions)
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
    private static ErrorOr<object?> ConvertToObjectResult(object errorOrResult)
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
    object? HandlerInstance = null);
