using System.Reflection;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;

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
/// </remarks>
public class CommandDispatcher(CommandRegistry registry, IWedaApplicationContext context)
{
    private readonly ILogger _logger = context.GetLogger<CommandDispatcher>();

    public async Task<ErrorOr<object?>> DispatchAsync(
        CommandEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var registration = registry.GetRegistration(envelope.CommandName);
        if (registration is null)
        {
            _logger.LogWarning("No handler found for command: {CommandName}", envelope.CommandName);
            return Errors.Command.HandlerNotFound($"No handler registered for command '{envelope.CommandName}'");
        }

        // Deserialize command to strongly-typed object
        object command;
        try
        {
            command = registry.DeserializeCommand(envelope, registration.CommandType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize command {CommandName}", envelope.CommandName);
            return Errors.Command.DeserializationFailed(ex.Message);
        }

        // Extract metadata from command (RespTopic, Timeout)
        var metadata = ExtractCommandMetadata(command, envelope.CommandName);

        // Send "Received" response if RespTopic is provided
        if (!string.IsNullOrEmpty(metadata.RespTopic))
        {
            await SendResponseAsync(metadata.RespTopic,
                CommandResponse.Received(context.SubNodeInfo.Id ?? "", envelope.CommandName));
        }

        try
        {
            var handler = registry.CreateHandler(registration);
            if (handler is null)
            {
                _logger.LogWarning("Failed to create handler for command: {CommandName}", envelope.CommandName);
                return Errors.Command.HandlerNotFound($"Failed to create handler for command '{envelope.CommandName}'");
            }

            var result = await handler(command, context, cancellationToken);

            // Send response based on result
            if (!string.IsNullOrEmpty(metadata.RespTopic))
            {
                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    var errorCode = $"{envelope.CommandName}.{firstError.Code}";
                    var errorMessage = firstError.Description;

                    // Use "Rejected" for validation errors, "Failed" for execution errors
                    var response = firstError.Type == ErrorType.Validation
                        ? CommandResponse.Rejected(context.SubNodeInfo.Id ?? "", envelope.CommandName, errorCode, errorMessage)
                        : CommandResponse.Failed(context.SubNodeInfo.Id ?? "", envelope.CommandName, errorCode, errorMessage);

                    await SendResponseAsync(metadata.RespTopic, response);
                }
                else
                {
                    await SendResponseAsync(metadata.RespTopic,
                        CommandResponse.Success(context.SubNodeInfo.Id ?? "", envelope.CommandName, result.Value));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandName} execution failed", envelope.CommandName);

            // Send "Failed" response on exception
            if (!string.IsNullOrEmpty(metadata.RespTopic))
            {
                await SendResponseAsync(metadata.RespTopic,
                    CommandResponse.Failed(context.SubNodeInfo.Id ?? "", envelope.CommandName,
                        $"{envelope.CommandName}.ExecutionFailed", ex.Message));
            }

            return Errors.Command.ExecutionFailed(ex.Message);
        }
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
