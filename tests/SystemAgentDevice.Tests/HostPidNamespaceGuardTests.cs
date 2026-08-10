using System.Runtime.InteropServices;

using Weda.SubNode.Core.Commands.Handlers.System;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Tests for the reboot/shutdown PID-namespace guard.
///
/// Background: on adlk.edgedevice.2 the system-agent container ran without <c>pid: host</c>.
/// nsenter -t 1 targeted the container's own init, and the reboot() fallback killed the
/// container's PID namespace instead of rebooting the machine (man 2 reboot, Linux >= 3.4).
/// Six reboot commands produced six container restarts and zero host reboots — while the
/// handler reported success every time, because reboot() returns 0 in that case.
/// </summary>
public class HostPidNamespaceGuardTests
{
    private static bool OnLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    [Fact]
    public void IsHostPidNamespace_AlwaysReportsAReason_WhenItReturnsFalse()
    {
        var ok = HostPidNamespaceGuard.IsHostPidNamespace(out var reason);

        if (!ok)
        {
            // A blocked reboot must explain itself — the operator has to know what to change.
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.Contains("pid: host", reason);
        }
    }

    [Fact]
    public void IsHostPidNamespace_OnATestHost_ReturnsTrue()
    {
        // The test process is never PID 1, so the guard must not block. This is the
        // regression that matters in the other direction: a guard that returns false
        // for normal processes would break reboot everywhere.
        var ok = HostPidNamespaceGuard.IsHostPidNamespace(out var reason);

        Assert.True(ok, $"guard unexpectedly blocked: {reason}");
    }

    [Fact]
    public void IsHostPidNamespace_IsDeterministic()
    {
        var first = HostPidNamespaceGuard.IsHostPidNamespace(out _);
        var second = HostPidNamespaceGuard.IsHostPidNamespace(out _);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsHostPidNamespace_OnNonLinux_ReturnsTrueWithNoReason()
    {
        if (OnLinux)
            return; // covered by the Linux-specific assertions above

        Assert.True(HostPidNamespaceGuard.IsHostPidNamespace(out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void ProcComm_DiscriminatesPid1FromSelf_OnLinux()
    {
        if (!OnLinux)
            return;

        // Pins the mechanism the guard relies on: /proc/1/comm is readable and, for any
        // process that is not its namespace's init, differs from /proc/self/comm.
        // (/proc/1/sched — the more common probe — is absent on kernels built without
        // CONFIG_SCHED_DEBUG, which is why the guard does not use it.)
        Assert.True(File.Exists("/proc/1/comm"));

        var pid1 = File.ReadAllText("/proc/1/comm").Trim();
        var self = File.ReadAllText("/proc/self/comm").Trim();

        Assert.False(string.IsNullOrEmpty(pid1));
        Assert.NotEqual(pid1, self);
    }
}
