using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Commands.Validation;

namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Dispatches commands to registered handlers and manages response lifecycle.
/// </summary>
/// <remarks>
/// Response handling follows these rules:
/// - If RespTopic is empty/null, no response is sent
/// - On dispatch start: send "Received" response
/// - On success: send "Success" response with result
/// - On error: send "Failed" or "Rejected" response with error details
///
/// Execution pipeline:
/// 1. Deserialization
/// 2. Send "Received" response
/// 3. DataAnnotation validation (always runs, outside pipeline)
/// 4. Pipeline Behaviors (based on handler attributes, sorted by DefaultPipeline order)
///    - [Validation] → ValidatorBehavior
///    - [Logging] → LoggingBehavior
/// 5. Handler execution
/// 6. Send Success/Failed response
/// </remarks>
public class CommandDispatcher(CommandRegistry registry, IWedaApplicationContext context)
{
    private readonly ILogger _logger = context.GetLogger<CommandDispatcher>();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> _behaviorMethodCache = [];

    public async Task<ErrorOr<object?>> DispatchAsync(
        CommandMessage message,
        CancellationToken cancellationToken = default)
    {
        // Extract command name from data
        var commandName = ExtractCommandName(message.Data);
        if (string.IsNullOrEmpty(commandName))
        {
            _logger.LogWarning("Command name not found in message data");
            return Errors.Command.DeserializationFailed("Command name (deviceCmd) not found in message data");
        }

        var registration = registry.GetRegistration(commandName);
        if (registration is null)
        {
            _logger.LogWarning("No handler found for command: {CommandName}", commandName);
            return Errors.Command.HandlerNotFound($"No handler registered for command '{commandName}'");
        }

        // Deserialize command to strongly-typed object
        object command;
        try
        {
            command = registry.DeserializeCommand(message.Data, registration.CommandType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize command {CommandName}", commandName);
            return Errors.Command.DeserializationFailed(ex.Message);
        }

        // Populate SeqId and ReqSeqId from message envelope for handlers that need them
        PopulateCommandMetadata(command, message.SeqId, message.ReqSeqId);

        // Extract metadata from command (RespTopic, Timeout)
        var metadata = ExtractCommandMetadata(command, commandName);

        // Send "Received" response if RespTopic is provided and auto ack is enabled
        if (!string.IsNullOrEmpty(metadata.RespTopic) && registration.AutoAckEnabled)
        {
            await SendResponseAsync(metadata.RespTopic,
                CommandResponse.Received(context.SubNodeInfo.Id ?? "", commandName, message.SeqId, message.ReqSeqId));
        }

        try
        {
            // Run DataAnnotation validation (always runs, outside pipeline)
            var validationResult = RunDataAnnotationValidation(registration.CommandType, command);
            if (validationResult.IsError)
            {
                // Send "Rejected" response for validation errors
                if (!string.IsNullOrEmpty(metadata.RespTopic))
                {
                    var firstError = validationResult.FirstError;
                    await SendResponseAsync(metadata.RespTopic,
                        CommandResponse.Rejected(context.SubNodeInfo.Id ?? "", commandName, message.SeqId,
                            CommandStatusCode.ValidationFailed,
                            firstError.Description, message.ReqSeqId));
                }
                return validationResult.Errors;
            }

            // Create handler delegate
            var handler = registry.CreateHandler(registration);
            if (handler is null)
            {
                _logger.LogWarning("Failed to create handler for command: {CommandName}", commandName);
                return Errors.Command.HandlerNotFound($"Failed to create handler for command '{commandName}'");
            }

            // Create behavior instances and build the pipeline
            var behaviors = registry.CreateBehaviors(registration);

            // Execute pipeline with behaviors
            var result = await ExecutePipelineAsync(
                command, registration, behaviors, handler, cancellationToken);

            // Send response based on result
            if (!string.IsNullOrEmpty(metadata.RespTopic))
            {
                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    var errorCode = $"{commandName}.{firstError.Code}";
                    var errorMessage = firstError.Description;

                    // Use "Rejected" for validation errors, "Failed" for execution errors
                    var response = firstError.Type == ErrorType.Validation
                        ? CommandResponse.Rejected(context.SubNodeInfo.Id ?? "", commandName, message.SeqId, CommandStatusCode.ValidationFailed, errorMessage, message.ReqSeqId)
                        : CommandResponse.Failed(context.SubNodeInfo.Id ?? "", commandName, message.SeqId, CommandStatusCode.HardwareError, errorMessage, message.ReqSeqId);

                    await SendResponseAsync(metadata.RespTopic, response);
                }
                else
                {
                    if (result.Value is IResult typedResult)
                    {
                        // Use IResult properties for structured response
                        var response = new CommandResponse
                        {
                            DeviceId = context.SubNodeInfo.Id ?? "",
                            SeqId = message.SeqId,
                            ReqSeqId = message.ReqSeqId,
                            Data = new CommandResponseData
                            {
                                DeviceCmd = commandName,
                                MsgType = "result",
                                Status = typedResult.Status,
                                Message = typedResult.Message,
                                ResultData = typedResult.ResultData,
                                ExecutedAt = typedResult.ExecutedAt,
                                CompletedAt = typedResult.CompletedAt
                            }
                        };
                        await SendResponseAsync(metadata.RespTopic, response);
                    }
                    else
                    {
                        // Fallback for non-IResult responses
                        await SendResponseAsync(metadata.RespTopic,
                            CommandResponse.Success(context.SubNodeInfo.Id ?? "", commandName, message.SeqId,
                                CommandStatusCode.Success, null, result.Value, message.ReqSeqId));
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandName} execution failed", commandName);

            // Send "Failed" response on exception
            if (!string.IsNullOrEmpty(metadata.RespTopic))
            {
                await SendResponseAsync(metadata.RespTopic,
                    CommandResponse.Failed(context.SubNodeInfo.Id ?? "", commandName, message.SeqId,
                        CommandStatusCode.HardwareError, ex.Message, message.ReqSeqId));
            }

            return Errors.Command.ExecutionFailed(ex.Message);
        }
    }

    /// <summary>
    /// Extracts the command name (deviceCmd) from the raw JSON data.
    /// </summary>
    private static string? ExtractCommandName(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (data.Value.TryGetProperty("deviceCmd", out var deviceCmd))
            return deviceCmd.GetString();

        return null;
    }

    /// <summary>
    /// Executes the pipeline with behaviors wrapping the handler.
    /// </summary>
    private async Task<ErrorOr<object?>> ExecutePipelineAsync(
        object command,
        CommandRegistration registration,
        IReadOnlyList<object> behaviors,
        Func<object, IWedaApplicationContext, CancellationToken, Task<ErrorOr<object?>>> handler,
        CancellationToken cancellationToken)
    {
        if (behaviors.Count == 0)
        {
            // No behaviors, execute handler directly
            return await handler(command, context, cancellationToken);
        }

        // Build the pipeline from inside out
        // The innermost function is the handler
        Func<Task<ErrorOr<object?>>> next = () => handler(command, context, cancellationToken);

        // Wrap with behaviors in reverse order (so first registered executes first)
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var currentNext = next;

            next = () => InvokeBehaviorAsync(
                behavior, command, registration, currentNext, cancellationToken);
        }

        return await next();
    }

    /// <summary>
    /// Invokes a single behavior's HandleAsync method using reflection.
    /// </summary>
    private async Task<ErrorOr<object?>> InvokeBehaviorAsync(
        object behavior,
        object command,
        CommandRegistration registration,
        Func<Task<ErrorOr<object?>>> next,
        CancellationToken cancellationToken)
    {
        // Find the HandleAsync method
        // var handleMethod = behavior.GetType().GetMethod("HandleAsync");
        var handleMethod = _behaviorMethodCache.GetOrAdd(
            behavior.GetType(),
            type => type.GetMethod("HandleAsync"));

        if (handleMethod is null)
        {
            _logger.LogWarning("Behavior {BehaviorType} does not have HandleAsync method",
                behavior.GetType().Name);
            return await next();
        }

        // Create a typed 'next' delegate for the behavior
        // IPipelineBehavior<TCommand, TResult>.HandleAsync expects Func<Task<ErrorOr<TResult>>>
        var nextDelegate = CreateTypedNextDelegate(next, registration.ResultType);

        try
        {
            // Invoke: HandleAsync(command, context, next, cancellationToken)
            var task = handleMethod.Invoke(behavior, [command, context, nextDelegate, cancellationToken]);
            if (task is null)
            {
                return await next();
            }

            await ((Task)task).ConfigureAwait(false);

            // Get the result
            var resultProperty = task.GetType().GetProperty("Result");
            var result = resultProperty?.GetValue(task);

            if (result is null)
            {
                return Errors.Command.ExecutionFailed("Behavior returned null result");
            }

            return CommandRegistry.ConvertToObjectResult(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    /// <summary>
    /// Creates a typed Func&lt;Task&lt;ErrorOr&lt;TResult&gt;&gt;&gt; delegate from Func&lt;Task&lt;ErrorOr&lt;object?&gt;&gt;&gt;.
    /// </summary>
    private static object CreateTypedNextDelegate(Func<Task<ErrorOr<object?>>> next, Type resultType)
    {
        // We need to create a Func<Task<ErrorOr<TResult>>> that wraps our Func<Task<ErrorOr<object?>>>
        // This is done by creating an async lambda that calls next() and converts the result

        var funcType = typeof(Func<>).MakeGenericType(
            typeof(Task<>).MakeGenericType(
                typeof(ErrorOr<>).MakeGenericType(resultType)));

        // Create a wrapper method dynamically
        var wrapperMethod = typeof(CommandDispatcher)
            .GetMethod(nameof(CreateNextWrapper), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(resultType);

        return wrapperMethod.Invoke(null, [next])!;
    }

    /// <summary>
    /// Helper method to create a typed next wrapper.
    /// </summary>
    private static Func<Task<ErrorOr<TResult>>> CreateNextWrapper<TResult>(Func<Task<ErrorOr<object?>>> next)
    {
        return async () =>
        {
            var result = await next();
            if (result.IsError)
            {
                return result.Errors;
            }

            // Convert object? to TResult
            if (result.Value is TResult typedValue)
            {
                return typedValue;
            }

            if (result.Value is null && !typeof(TResult).IsValueType)
            {
                return default!;
            }

            // Try to cast
            try
            {
                return (TResult)result.Value!;
            }
            catch
            {
                return Errors.Command.ExecutionFailed($"Cannot convert result to {typeof(TResult).Name}");
            }
        };
    }

    /// <summary>
    /// Runs DataAnnotation validation on the command.
    /// This always runs before the pipeline behaviors.
    /// </summary>
    private ErrorOr<Success> RunDataAnnotationValidation(Type commandType, object command)
    {
        // Create DataAnnotationValidator<TCommand> dynamically
        var validatorType = typeof(DataAnnotationValidator<>).MakeGenericType(commandType);
        var validator = Activator.CreateInstance(validatorType);

        if (validator is null)
        {
            return Result.Success;
        }

        var validateMethod = validatorType.GetMethod("Validate");
        if (validateMethod is null)
        {
            return Result.Success;
        }

        var result = validateMethod.Invoke(validator, [command]);
        if (result is null)
        {
            return Result.Success;
        }

        return CommandRegistry.ConvertToObjectResult(result).Match<ErrorOr<Success>>(
            value => Result.Success,
            errors => errors);
    }

    /// <summary>
    /// Populates SeqId and ReqSeqId from the message envelope into the command object.
    /// This allows handlers to access envelope metadata for custom response handling.
    /// </summary>
    private static void PopulateCommandMetadata(object command, ulong seqId, string? reqSeqId)
    {
        var type = command.GetType();

        var seqIdProperty = type.GetProperty("SeqId", BindingFlags.Public | BindingFlags.Instance);
        seqIdProperty?.SetValue(command, seqId);

        var reqSeqIdProperty = type.GetProperty("ReqSeqId", BindingFlags.Public | BindingFlags.Instance);
        reqSeqIdProperty?.SetValue(command, reqSeqId);
    }

    /// <summary>
    /// Extracts command metadata (RespTopic, Timeout) from the command object using reflection.
    /// Convention: looks for properties named "RespTopic" and "Timeout".
    /// </summary>
    private static CommandMetadata ExtractCommandMetadata(object command, string commandName)
    {
        var type = command.GetType();

        // Extract RespTopic (convention-based)
        var respTopicProperty = type.GetProperty("RespTopic", BindingFlags.Public | BindingFlags.Instance);
        var respTopic = respTopicProperty?.GetValue(command) as string ?? string.Empty;

        // Extract Timeout (convention-based)
        var timeoutProperty = type.GetProperty("Timeout", BindingFlags.Public | BindingFlags.Instance);
        var timeout = timeoutProperty?.GetValue(command) is int t ? t : 300;

        return new CommandMetadata(commandName, respTopic, timeout);
    }

    private async Task SendResponseAsync(string respTopic, CommandResponse response)
    {
        try
        {
            await context.CloudService.SendCommandResponseAsync(respTopic, response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send command response to {RespTopic}", respTopic);
        }
    }
}

/// <summary>
/// Command metadata extracted from the command object.
/// </summary>
internal record CommandMetadata(string CommandName, string RespTopic, int Timeout);