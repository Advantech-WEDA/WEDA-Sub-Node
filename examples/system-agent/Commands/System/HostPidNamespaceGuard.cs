using System.Runtime.InteropServices;

namespace Weda.SubNode.Core.Commands.Handlers.System;

/// <summary>
/// Detects whether the agent shares the host's PID namespace, which both reboot and
/// shutdown depend on.
/// </summary>
/// <remarks>
/// <para>
/// Without <c>pid: host</c> in the container's compose definition, neither escalation path
/// reaches the host:
/// </para>
/// <list type="bullet">
///   <item><c>nsenter -t 1</c> targets PID 1 of the <em>container's</em> namespace — the agent
///         process itself — not the host's init.</item>
///   <item>Since Linux 3.4, <c>reboot()</c> called from a non-initial PID namespace kills that
///         namespace's init instead of rebooting the machine (see <c>man 2 reboot</c>). The
///         container dies and is restarted by its restart policy; the host stays up.</item>
/// </list>
/// <para>
/// The syscall reports success in that case, so without this guard the agent acknowledges the
/// command, restarts itself, and the operator sees a successful reboot that never happened.
/// </para>
/// </remarks>
internal static class HostPidNamespaceGuard
{
    /// <summary>
    /// Returns true when PID 1 is a process other than this one, i.e. the agent is not the init
    /// of its own PID namespace and reboot/shutdown escalation can reach the host.
    /// </summary>
    /// <remarks>
    /// Compares <c>/proc/1/comm</c> with <c>/proc/self/comm</c>. <c>/proc/1/sched</c> — the more
    /// common namespace probe — is unavailable on kernels built without <c>CONFIG_SCHED_DEBUG</c>
    /// (verified absent on the Tegra 5.15 target), so it cannot be relied on here.
    ///
    /// Non-Linux platforms and an unreadable <c>/proc</c> return true: the caller's existing
    /// platform check owns the former, and an unreadable <c>/proc</c> must not block a legitimate
    /// reboot.
    ///
    /// Known limitation: an agent deliberately run as the host's init would be misreported as
    /// namespaced. That is not a supported deployment for this example.
    /// </remarks>
    /// <param name="reason">Human-readable explanation when the check fails; empty otherwise.</param>
    internal static bool IsHostPidNamespace(out string reason)
    {
        reason = string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return true;

        string pid1;
        string self;
        try
        {
            pid1 = global::System.IO.File.ReadAllText("/proc/1/comm").Trim();
            self = global::System.IO.File.ReadAllText("/proc/self/comm").Trim();
        }
        catch (global::System.Exception ex)
        {
            // Cannot determine — assume the deployment is correct rather than block the operation.
            reason = $"Could not read /proc to verify PID namespace: {ex.Message}";
            return true;
        }

        if (string.IsNullOrEmpty(pid1) || !string.Equals(pid1, self, global::System.StringComparison.Ordinal))
            return true;

        reason =
            $"Agent is PID 1 of its own PID namespace (/proc/1/comm='{pid1}'). " +
            "The container is missing 'pid: host', so nsenter cannot reach the host's init and " +
            "reboot() would kill this container instead of rebooting the machine. " +
            "Add 'pid: host' to the system-agent service in docker-compose.yml.";
        return false;
    }
}
