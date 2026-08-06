using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Core.Commands.Handlers.System;

/// <summary>
/// Reboots or powers off the host by writing to <c>/proc/sysrq-trigger</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the lowest-dependency escalation path available to a containerised agent. Unlike
/// <c>nsenter</c> and the <c>reboot()</c> syscall it needs no <c>pid: host</c> — sysrq is a global
/// kernel action and is not PID-namespaced — no <c>nsenter</c> binary, no host <c>/bin/sh</c> or
/// <c>reboot</c> executable, and no P/Invoke, so it is immune to the container/host glibc version
/// skew that breaks dynamically linked host tooling.
/// </para>
/// <para>Requirements, all verifiable before use:</para>
/// <list type="number">
///   <item>Kernel built with <c>CONFIG_MAGIC_SYSRQ</c> — otherwise the file does not exist.</item>
///   <item><c>kernel.sysrq</c> permitting reboot/poweroff (bit <c>0x80</c>), sync (<c>0x10</c>) and
///         remount-read-only (<c>0x20</c>); value <c>1</c> enables everything.</item>
///   <item>Running as root — the file is mode 0200, root-owned.</item>
///   <item><c>/proc</c> mounted read-write.</item>
///   <item><c>/proc/sysrq-trigger</c> not mounted read-only. runc mounts it <c>ro</c> by default;
///         <c>privileged: true</c> drops that protection.</item>
/// </list>
/// <para>
/// Trade-off: sysrq bypasses the init system, so units and containers receive no SIGTERM and get no
/// chance to shut down cleanly. The sync + remount-read-only prefix protects filesystem integrity
/// but not application-level cleanup. Callers wanting a graceful stop should try an init-system
/// route first and fall back to this.
/// </para>
/// </remarks>
internal static class SysrqPowerControl
{
    private const string TriggerPath = "/proc/sysrq-trigger";
    private const string SysrqEnabledPath = "/proc/sys/kernel/sysrq";

    /// <summary>Bit 0x80 in kernel.sysrq: allow reboot/poweroff.</summary>
    private const int RebootPoweroffMask = 0x80;

    /// <summary>Emergency sync: flush dirty buffers to disk.</summary>
    private const string CmdSync = "s";

    /// <summary>Remount all filesystems read-only.</summary>
    private const string CmdRemountReadOnly = "u";

    /// <summary>Immediately reboot, without syncing or unmounting.</summary>
    private const string CmdReboot = "b";

    /// <summary>Immediately power off.</summary>
    private const string CmdPowerOff = "o";

    /// <summary>Grace period for the emergency sync to drain before remounting.</summary>
    private static readonly TimeSpan SyncSettleDelay = TimeSpan.FromSeconds(3);

    /// <summary>Grace period for the remount to complete before the final trigger.</summary>
    private static readonly TimeSpan RemountSettleDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long to wait after the reboot/poweroff trigger before concluding it did not take effect.
    /// A successful trigger never returns from here — the machine goes down.
    /// </summary>
    private static readonly TimeSpan PostTriggerGrace = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Checks every precondition without performing any action.
    /// </summary>
    /// <param name="reason">Why sysrq is unusable; empty when it is available.</param>
    internal static bool IsAvailable(out string reason)
    {
        reason = string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            reason = "sysrq is Linux-only";
            return false;
        }

        if (!global::System.IO.File.Exists(TriggerPath))
        {
            reason = $"{TriggerPath} not present (kernel built without CONFIG_MAGIC_SYSRQ)";
            return false;
        }

        if (!IsRebootBitEnabled(out var maskReason))
        {
            reason = maskReason;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads kernel.sysrq and reports whether reboot/poweroff is permitted.
    /// </summary>
    /// <remarks>
    /// Kernel sources suggest writes through <c>/proc/sysrq-trigger</c> bypass this mask
    /// (<c>write_sysrq_trigger</c> calls <c>__handle_sysrq(c, false)</c>, i.e. <c>check_mask=false</c>),
    /// which would make the sysctl irrelevant here. That was not verifiable on the target device, so
    /// the mask is treated as load-bearing: one file read, and it fails safe toward the other paths.
    /// An unreadable or unparsable value is treated as permitted rather than blocking a reboot.
    /// </remarks>
    private static bool IsRebootBitEnabled(out string reason)
    {
        string? raw = null;
        try
        {
            raw = global::System.IO.File.ReadAllText(SysrqEnabledPath);
        }
        catch (global::System.Exception)
        {
            // Cannot read the policy — do not block on it.
        }

        return IsRebootPermittedByMask(raw, out reason);
    }

    /// <summary>
    /// Decides whether a raw <c>kernel.sysrq</c> value permits reboot/poweroff.
    /// </summary>
    /// <remarks>
    /// Split out from the file read so the semantics are testable without a writable
    /// <c>/proc</c>. Fails open: a null, empty or unparsable value returns true rather than
    /// blocking a commanded reboot on a policy that could not be read.
    /// </remarks>
    /// <param name="raw">Raw file contents, or null when unreadable.</param>
    /// <param name="reason">Why reboot is not permitted; empty when it is.</param>
    internal static bool IsRebootPermittedByMask(string? raw, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var value))
            return true;

        // 1 enables every function; 0 disables sysrq entirely; anything else is a bitmask.
        if (value == 1 || (value > 1 && (value & RebootPoweroffMask) != 0))
            return true;

        reason = value == 0
            ? $"kernel.sysrq is 0 (sysrq disabled); set it to 1 or a mask including 0x{RebootPoweroffMask:X}"
            : $"kernel.sysrq is {value}, which omits the reboot/poweroff bit (0x{RebootPoweroffMask:X})";
        return false;
    }

    /// <summary>Reboots the host. Returns false only if the trigger did not take effect.</summary>
    internal static bool TryReboot(ILogger logger) => TryPowerAction(CmdReboot, "reboot", logger);

    /// <summary>Powers off the host. Returns false only if the trigger did not take effect.</summary>
    internal static bool TryPowerOff(ILogger logger) => TryPowerAction(CmdPowerOff, "power off", logger);

    /// <summary>
    /// Runs the sync → remount-read-only → action sequence.
    /// </summary>
    /// <remarks>
    /// Sync and remount are best-effort: if either fails the action still proceeds, because a
    /// commanded reboot that does not happen is worse than one that skips a flush. The final
    /// trigger is the only step whose failure is reported.
    /// </remarks>
    private static bool TryPowerAction(string command, string description, ILogger logger)
    {
        if (!IsAvailable(out var unavailableReason))
        {
            logger.LogWarning("sysrq {Description} unavailable: {Reason}", description, unavailableReason);
            return false;
        }

        // Protect filesystem integrity before the hard action. sysrq does not stop services,
        // so this is the only cleanup available on this path.
        if (TryWrite(CmdSync, logger, "emergency sync"))
            Thread.Sleep(SyncSettleDelay);

        if (TryWrite(CmdRemountReadOnly, logger, "remount read-only"))
            Thread.Sleep(RemountSettleDelay);

        logger.LogWarning("Executing {Description} via sysrq", description);
        if (!TryWrite(command, logger, description))
            return false;

        // A successful trigger takes the machine down; reaching this point means it did not.
        Thread.Sleep(PostTriggerGrace);
        logger.LogWarning(
            "Still running {Seconds}s after the sysrq {Description} trigger — it did not take effect",
            PostTriggerGrace.TotalSeconds, description);
        return false;
    }

    private static bool TryWrite(string command, ILogger logger, string description)
    {
        try
        {
            // Must be a single write of the bare command character; the kernel reads one byte.
            global::System.IO.File.WriteAllText(TriggerPath, command);
            return true;
        }
        catch (global::System.Exception ex)
        {
            logger.LogWarning(
                "sysrq {Description} ('{Command}') failed: {Message}. " +
                "Requires root, a read-write /proc, and /proc/sysrq-trigger not mounted read-only " +
                "(runc mounts it read-only unless the container is privileged)",
                description, command, ex.Message);
            return false;
        }
    }
}
