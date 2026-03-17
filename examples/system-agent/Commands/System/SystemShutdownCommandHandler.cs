using System.Diagnostics;
using System.Runtime.InteropServices;

using ErrorOr;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Commands.Handlers.System.Models;

namespace Weda.SubNode.Core.Commands.Handlers.System;

/// <summary>
/// Handler for the "system.shutdown" command that initiates a safe system shutdown.
/// </summary>
/// <remarks>
/// This handler:
/// 1. Immediately publishes an asynchronous ACK response.
/// 2. Publishes a result response indicating the shutdown is commencing.
/// 3. Initiates a system shutdown after an optional delay.
///
/// Auto-ack is disabled because the handler sends its own custom ACK.
/// </remarks>
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class SystemShutdownCommandHandler : ICommandHandler<SystemShutdownCommand, SystemShutdownResult>
{
    public async Task<ErrorOr<SystemShutdownResult>> HandleAsync(
        SystemShutdownCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SystemShutdownCommandHandler>();
        var cloudService = context.CloudService;
        var deviceId = context.SubNodeInfo.Id;
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var delaySeconds = Math.Max(command.Parameters.DelaySeconds, 0);

        logger.LogInformation("Received system.shutdown command with {DelaySeconds}s delay", delaySeconds);

        var resultData = new SystemCommandResultData
        {
            Operation = "shutdown",
            DelaySeconds = delaySeconds
        };

        // 1. Send immediate ACK
        await SendAckAsync(cloudService, deviceId ?? "", command.RespTopic, command.DeviceCmd,
            command.SeqId, command.ReqSeqId, resultData, logger, cancellationToken);

        // 2. Verify platform support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogError(
                "System shutdown via container is only supported on Linux. Current platform: {OS}",
                RuntimeInformation.OSDescription);
            return SystemShutdownResult.Error(
                SystemCommandStatusCode.NotSupported,
                $"System shutdown via container is only supported on Linux. Current platform: {RuntimeInformation.OSDescription}",
                executedAt);
        }

        // 3. Initiate shutdown in the background so we can return the result response first
        _ = Task.Run(async () =>
        {
            try
            {
                if (delaySeconds > 0)
                {
                    logger.LogInformation("Waiting {DelaySeconds}s before initiating shutdown...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }

                logger.LogWarning("Initiating system shutdown NOW");
                ExecuteShutdown(logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initiate system shutdown");
            }
        }, CancellationToken.None);

        // 4. Return result indicating shutdown is commencing
        return SystemShutdownResult.Success(resultData, executedAt);
    }

    /// <summary>
    /// Executes the shutdown command via nsenter to shut down the host machine from within a container.
    /// Requires docker-compose: privileged: true + pid: host
    /// </summary>
    private static void ExecuteShutdown(ILogger logger)
    {
        try
        {
            // nsenter -t 1 enters PID 1's (host init) mount/uts/ipc/net namespaces,
            // then executes /sbin/shutdown on the host.
            Process.Start(new ProcessStartInfo
            {
                FileName = "nsenter",
                Arguments = "-t 1 -m -u -i -n -- /sbin/shutdown -h now",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute shutdown command via nsenter");
        }
    }

    private static async Task SendAckAsync(
        Abstractions.Cloud.IWedaCloudService cloudService,
        string deviceId,
        string? respTopic,
        string deviceCmd,
        ulong seqId,
        string? reqSeqId,
        SystemCommandResultData resultData,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(respTopic))
            return;

        try
        {
            var response = new CommandResponse
            {
                DeviceId = deviceId,
                SeqId = seqId,
                ReqSeqId = reqSeqId,
                Data = new CommandResponseData
                {
                    DeviceCmd = deviceCmd,
                    MsgType = "ack",
                    Status = SystemCommandStatusCode.Success,
                    Message = "Shutdown command received, operation commencing",
                    ResultData = resultData
                }
            };

            await cloudService.SendCommandResponseAsync(respTopic, response, cancellationToken);
            logger.LogDebug("ACK sent for system.shutdown command");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send ACK for system.shutdown");
        }
    }
}
