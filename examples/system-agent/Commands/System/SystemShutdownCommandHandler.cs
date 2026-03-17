using System.Management;
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
/// On Linux: uses libc reboot() syscall with POWER_OFF (requires privileged container + pid: host).
/// On Windows: uses WMI Win32_OperatingSystem.Win32Shutdown.
///
/// Auto-ack is disabled because the handler sends its own custom ACK.
/// </remarks>
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class SystemShutdownCommandHandler : ICommandHandler<SystemShutdownCommand, SystemShutdownResult>
{
    [DllImport("libc.so.6", SetLastError = true)]
    private static extern int reboot(int magic, int magic2, int cmd, IntPtr arg);

    private const int LINUX_REBOOT_MAGIC1 = unchecked((int)0xfee1dead);
    private const int LINUX_REBOOT_MAGIC2 = 672274793;
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

        // 2. Initiate shutdown in the background so we can return the result response first
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

        // 3. Return result indicating shutdown is commencing
        return SystemShutdownResult.Success(resultData, executedAt);
    }

    /// <summary>
    /// Executes the shutdown command using platform-native APIs.
    /// Linux: libc reboot() syscall with POWER_OFF. Requires privileged container + pid: host.
    /// Windows: WMI Win32Shutdown with Forced Shutdown flag.
    /// </summary>
    private static void ExecuteShutdown(ILogger logger)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                int ret = reboot(LINUX_REBOOT_MAGIC1, LINUX_REBOOT_MAGIC2, LINUX_REBOOT_CMD_POWER_OFF, IntPtr.Zero);
                if (ret != 0)
                {
                    logger.LogError("Linux shutdown syscall failed, errno={Errno}", Marshal.GetLastWin32Error());
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var mc = new ManagementClass("Win32_OperatingSystem");
                mc.Get();
                mc.Scope.Options.EnablePrivileges = true;

                foreach (ManagementObject mo in mc.GetInstances())
                {
                    // Flag 5 = Forced Shutdown
                    mo.InvokeMethod("Win32Shutdown", new object[] { 5, 0 });
                }
            }
            else
            {
                logger.LogError("System shutdown is not supported on this platform: {OS}", RuntimeInformation.OSDescription);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute shutdown command");
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
