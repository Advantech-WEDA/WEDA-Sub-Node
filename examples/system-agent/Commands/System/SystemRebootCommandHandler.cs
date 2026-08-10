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
/// Escalation order, cheapest dependencies first:
/// 1. sysrq (/proc/sysrq-trigger) — syncs and remounts read-only, then resets. Needs only a
///    privileged container and CONFIG_MAGIC_SYSRQ; notably NOT pid: host, since sysrq is not
///    PID-namespaced. Does not notify host services.
/// 2. nsenter into the host namespace (graceful, via the host's init system).
/// 3. libc reboot() syscall (hard reboot, does not notify host services).
/// Paths 2 and 3 additionally require docker-compose: privileged: true + pid: host.
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

        // 2b. Verify at least one escalation path can reach the host. sysrq needs no pid: host;
        // nsenter and reboot() both do, and without it they silently restart this container
        // instead of rebooting the machine. Fail only when neither path is usable, so the
        // operator never gets a success for a reboot that cannot happen.
        var sysrqAvailable = SysrqPowerControl.IsAvailable(out var sysrqReason);
        var hostPidNamespace = HostPidNamespaceGuard.IsHostPidNamespace(out var pidNsReason);

        if (!sysrqAvailable && !hostPidNamespace)
        {
            var detail = $"sysrq unavailable ({sysrqReason}); {pidNsReason}";
            logger.LogError("Cannot reboot host: {Reason}", detail);
            return SystemRebootResult.Error(
                SystemCommandStatusCode.NotSupported,
                $"Cannot reboot host: {detail}",
                executedAt);
        }

        if (!sysrqAvailable)
        {
            logger.LogWarning(
                "sysrq unavailable ({Reason}); falling back to nsenter/reboot(), which require pid: host",
                sysrqReason);
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
    /// Executes the reboot command, trying each escalation path until one takes effect.
    /// 1. sysrq — sync, remount read-only, reset. No pid: host required.
    /// 2. nsenter into host PID 1 namespace for a graceful reboot via the host's init system.
    /// 3. libc reboot() syscall (hard reboot).
    /// Paths 2 and 3 require docker-compose: privileged: true + pid: host.
    /// </summary>
    private static void ExecuteReboot(ILogger logger)
    {
        // Primary: sysrq. Fewest dependencies of the three — no pid: host (sysrq is not
        // PID-namespaced), no nsenter binary, no host /bin/sh, no P/Invoke, and immune to
        // container/host glibc skew. Syncs and remounts read-only before triggering.
        if (SysrqPowerControl.TryReboot(logger))
            return;

        // Secondary: nsenter (graceful reboot via host's init/systemd)
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
