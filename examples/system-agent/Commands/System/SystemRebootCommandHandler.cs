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
/// Handler for the "system.reboot" command that initiates a safe system reboot.
/// </summary>
/// <remarks>
/// This handler:
/// 1. Immediately publishes an asynchronous ACK response.
/// 2. Publishes a result response indicating the reboot is commencing.
/// 3. Initiates a system reboot after an optional delay.
///
/// Primary: nsenter into host namespace (graceful, via host's init system).
/// Fallback: libc reboot() syscall (hard reboot, does not notify host services).
/// Both require docker-compose: privileged: true + pid: host.
///
/// Auto-ack is disabled because the handler sends its own custom ACK.
/// </remarks>
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class SystemRebootCommandHandler : ICommandHandler<SystemRebootCommand, SystemRebootResult>
{
    // glibc reboot() wrapper takes only 1 argument (cmd). Magic numbers are handled internally by glibc.
    // See: https://man7.org/linux/man-pages/man2/reboot.2.html
    [DllImport("libc.so.6", SetLastError = true)]
    private static extern int reboot(int cmd);

    private const int LINUX_REBOOT_CMD_RESTART = 0x01234567;

    public async Task<ErrorOr<SystemRebootResult>> HandleAsync(
        SystemRebootCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SystemRebootCommandHandler>();
        var cloudService = context.CloudService;
        var deviceId = context.SubNodeInfo.Id;
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var delaySeconds = Math.Max(command.Parameters.DelaySeconds, 0);

        logger.LogInformation("Received system.reboot command with {DelaySeconds}s delay", delaySeconds);

        var resultData = new SystemCommandResultData
        {
            Operation = "reboot",
            DelaySeconds = delaySeconds
        };

        // 1. Send immediate ACK
        await SendAckAsync(cloudService, deviceId ?? "", command.RespTopic, command.DeviceCmd,
            command.SeqId, command.ReqSeqId, resultData, logger, cancellationToken);

        // 2. Verify platform support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogError(
                "System reboot via container is only supported on Linux. Current platform: {OS}",
                RuntimeInformation.OSDescription);
            return SystemRebootResult.Error(
                SystemCommandStatusCode.NotSupported,
                $"System reboot via container is only supported on Linux. Current platform: {RuntimeInformation.OSDescription}",
                executedAt);
        }

        // 3. Initiate reboot in the background so we can return the result response first
        _ = Task.Run(async () =>
        {
            try
            {
                if (delaySeconds > 0)
                {
                    logger.LogInformation("Waiting {DelaySeconds}s before initiating reboot...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }

                logger.LogWarning("Initiating system reboot NOW");
                ExecuteReboot(logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initiate system reboot");
            }
        }, CancellationToken.None);

        // 4. Return result indicating reboot is commencing
        return SystemRebootResult.Success(resultData, executedAt);
    }

    /// <summary>
    /// Executes the reboot command.
    /// Primary: nsenter into host PID 1 namespace for graceful reboot via host's init system.
    /// Fallback: libc reboot() syscall (hard reboot if nsenter is unavailable).
    /// Requires docker-compose: privileged: true + pid: host.
    /// </summary>
    private static void ExecuteReboot(ILogger logger)
    {
        // Primary: nsenter (graceful reboot via host's init/systemd)
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nsenter",
                Arguments = "-t 1 -m -u -i -n -- /bin/sh -c reboot",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            });

            if (process is not null)
            {
                process.WaitForExit(10_000);
                if (process.ExitCode == 0)
                {
                    logger.LogInformation("Reboot initiated via nsenter (graceful)");
                    return;
                }

                var stderr = process.StandardError.ReadToEnd();
                logger.LogWarning("nsenter reboot failed (exit={ExitCode}): {Error}. Falling back to libc reboot() syscall",
                    process.ExitCode, stderr.Trim());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "nsenter not available. Falling back to libc reboot() syscall");
        }

        // Fallback: libc reboot() syscall (hard reboot)
        try
        {
            logger.LogWarning("Executing hard reboot via libc reboot() syscall");
            int ret = reboot(LINUX_REBOOT_CMD_RESTART);
            if (ret != 0)
            {
                logger.LogError("libc reboot() syscall failed, errno={Errno}", Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute reboot via libc syscall");
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
                    Message = "Reboot command received, operation commencing",
                    ResultData = resultData
                }
            };

            await cloudService.SendCommandResponseAsync(respTopic, response, cancellationToken);
            logger.LogDebug("ACK sent for system.reboot command");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send ACK for system.reboot");
        }
    }
}
