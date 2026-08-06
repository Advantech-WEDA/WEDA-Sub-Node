using System.Runtime.InteropServices;

using Weda.SubNode.Core.Commands.Handlers.System;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Tests for the sysrq reboot/shutdown path.
///
/// sysrq is the first escalation path because it has the fewest prerequisites: it is not
/// PID-namespaced (so it needs no <c>pid: host</c>), needs no nsenter binary, no host /bin/sh
/// and no P/Invoke — the last of which is what produced the original
/// "GLIBC_2.38 not found (required by nsenter)" failure on adlk.edgedevice.2.
///
/// NOTE: these tests never invoke TryReboot/TryPowerOff. Those write to /proc/sysrq-trigger and
/// would reset the machine running the suite. Only the pure decision logic and the read-only
/// availability probe are exercised.
/// </summary>
public class SysrqPowerControlTests
{
    // --- kernel.sysrq mask semantics ---

    [Theory]
    [InlineData("1")]        // all functions enabled
    [InlineData("176")]      // 0xB0 = sync | remount-ro | reboot  (Ubuntu's 10-magic-sysrq.conf)
    [InlineData("438")]      // 0x1B6, includes 0x80
    [InlineData("128")]      // 0x80 exactly: reboot/poweroff only
    [InlineData(" 1\n")]     // trailing newline from /proc
    public void IsRebootPermittedByMask_PermittingValues_ReturnTrue(string raw)
    {
        Assert.True(SysrqPowerControl.IsRebootPermittedByMask(raw, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void IsRebootPermittedByMask_Zero_IsRejectedAndExplained()
    {
        Assert.False(SysrqPowerControl.IsRebootPermittedByMask("0", out var reason));
        Assert.Contains("disabled", reason);
        Assert.Contains("0x80", reason);
    }

    [Theory]
    [InlineData("16")]   // sync only
    [InlineData("48")]   // sync | remount-ro, still no reboot bit
    [InlineData("2")]    // console loglevel only
    public void IsRebootPermittedByMask_MaskWithoutRebootBit_IsRejected(string raw)
    {
        Assert.False(SysrqPowerControl.IsRebootPermittedByMask(raw, out var reason));
        Assert.Contains("omits the reboot/poweroff bit", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public void IsRebootPermittedByMask_UnreadableOrUnparsable_FailsOpen(string? raw)
    {
        // A policy we cannot read must not block a commanded reboot — the write itself
        // will fail loudly if it is genuinely not permitted.
        Assert.True(SysrqPowerControl.IsRebootPermittedByMask(raw, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    // --- availability probe (read-only, safe) ---

    [Fact]
    public void IsAvailable_NeverThrows_AndExplainsItselfWhenUnavailable()
    {
        var available = SysrqPowerControl.IsAvailable(out var reason);

        if (available)
            Assert.Equal(string.Empty, reason);
        else
            Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void IsAvailable_OnNonLinux_IsUnavailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        Assert.False(SysrqPowerControl.IsAvailable(out var reason));
        Assert.Contains("Linux", reason);
    }

    [Fact]
    public void IsAvailable_OnLinuxWithSysrqCompiledIn_TracksTheKernelPolicy()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !File.Exists("/proc/sysrq-trigger"))
            return;

        // The probe must agree with the mask logic applied to the live sysctl. This pins the
        // two halves together so a change to either is caught.
        string? raw = null;
        try { raw = File.ReadAllText("/proc/sys/kernel/sysrq"); } catch { /* fails open below */ }

        var expected = SysrqPowerControl.IsRebootPermittedByMask(raw, out _);

        Assert.Equal(expected, SysrqPowerControl.IsAvailable(out _));
    }

    [Fact]
    public void IsAvailable_IsDeterministic()
    {
        Assert.Equal(
            SysrqPowerControl.IsAvailable(out _),
            SysrqPowerControl.IsAvailable(out _));
    }
}
