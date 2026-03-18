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
/// Primary: nsenter into host namespace (graceful, via host's init system).
/// Fallback: libc reboot() syscall with POWER_OFF (hard shutdown, does not notify host services).
/// Both require docker-compose: privileged: true + pid: host.
///
/// Auto-ack is disabled because the handler sends its own custom ACK.
/// </remarks>
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class SystemShutdownCommandHandler : ICommandHandler<SystemShutdownCommand, SystemShutdownResult>
{
    // glibc reboot() wrapper takes only 1 argument (cmd). Magic numbers are handled internally by glibc.
    // See: https://man7.org/linux/man-pages/man2/reboot.2.html
    [DllImport("libc.so.6", SetLastError = true)]
    private static extern int reboot(int cmd);

    private const int LINUX_REBOOT_CMD_POWER_OFF = unchecked((int)0x4321fedc);

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
    /// Executes the shutdown command.
    /// Primary: nsenter into host PID 1 namespace for graceful shutdown via host's init system.
    /// Fallback: libc reboot() syscall with POWER_OFF (hard shutdown if nsenter is unavailable).
    /// Requires docker-compose: privileged: true + pid: host.
    /// </summary>
    private static void ExecuteShutdown(ILogger logger)
    {
        // Primary: nsenter (graceful shutdown via host's init/systemd)
        try
        {
            var psi = new ProcessStartInfo("nsenter")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            // Use ArgumentList to avoid shell quoting issues with multi-word commands
            psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add("/bin/sh");
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("shutdown -h now"); // passed as a single token to /bin/sh -c

            var process = Process.Start(psi);
            if (process is not null)
            {
                process.WaitForExit(10_000);
                if (process.ExitCode == 0)
                {
                    logger.LogInformation("Shutdown initiated via nsenter (graceful)");
                    return;
                }

                var stderr = process.StandardError.ReadToEnd();
                logger.LogWarning("nsenter shutdown failed (exit={ExitCode}): {Error}. Falling back to libc reboot() syscall",
                    process.ExitCode, stderr.Trim());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "nsenter not available. Falling back to libc reboot() syscall");
        }

        // Fallback: libc reboot() syscall with POWER_OFF (hard shutdown)
        try
        {
            logger.LogWarning("Executing hard shutdown via libc reboot() syscall");
            int ret = reboot(LINUX_REBOOT_CMD_POWER_OFF);
            if (ret != 0)
            {
                logger.LogError("libc reboot() syscall (POWER_OFF) failed, errno={Errno}", Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute shutdown via libc syscall");
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
