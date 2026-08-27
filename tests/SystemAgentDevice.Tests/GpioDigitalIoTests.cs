using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using SystemAgentExample.Devices;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Unit tests for GpioDigitalIo: direction guard and write-then-verify,
/// exercised against an in-memory fake pin table (no hardware).
/// </summary>
public class GpioDigitalIoTests
{
    private readonly Dictionary<string, bool> _states = new();
    private readonly Dictionary<string, string> _directions = new();
    private int _setCalls;

    private GpioDigitalIo CreateIo(
        Func<string, bool?>? getLevel = null,
        Func<string, bool, bool>? setLevel = null)
    {
        return new GpioDigitalIo(
            getLevel ?? (p => _states.TryGetValue(p, out var s) ? s : null),
            setLevel ?? ((p, s) => { _setCalls++; _states[p] = s; return true; }),
            p => _directions.TryGetValue(p, out var d) ? d : null,
            NullLogger.Instance,
            verifyDelay: TimeSpan.Zero);
    }

    [Fact]
    public async Task SetOutputAsync_InputPin_ShouldRefuseWithoutWriting()
    {
        _directions["DI0"] = "input";
        var io = CreateIo();

        var result = await io.SetOutputAsync("DI0", true);

        result.ShouldBeFalse("input pins must not be writable");
        _setCalls.ShouldBe(0, "setLevel must not be called for a refused pin");
    }

    [Fact]
    public async Task SetOutputAsync_UnknownPin_ShouldRefuse()
    {
        var io = CreateIo();

        var result = await io.SetOutputAsync("NOPE", true);

        result.ShouldBeFalse("unknown pins have no direction and must be refused");
        _setCalls.ShouldBe(0);
    }

    [Fact]
    public async Task SetOutputAsync_VerifiedWrite_ShouldSucceed()
    {
        _directions["DO0"] = "output";
        var io = CreateIo();

        var result = await io.SetOutputAsync("DO0", true);

        result.ShouldBeTrue();
        _states["DO0"].ShouldBeTrue();
    }

    [Fact]
    public async Task SetOutputAsync_WriteFails_ShouldReturnFalse()
    {
        _directions["DO0"] = "output";
        var io = CreateIo(setLevel: (_, _) => false);

        var result = await io.SetOutputAsync("DO0", true);

        result.ShouldBeFalse("a failed hardware write must not report success");
    }

    [Fact]
    public async Task SetOutputAsync_ReadbackMismatch_ShouldFailAfterRetries()
    {
        _directions["DO0"] = "output";
        var readbacks = 0;
        var io = CreateIo(
            getLevel: _ => { readbacks++; return false; },   // never matches desired true
            setLevel: (_, _) => true);

        var result = await io.SetOutputAsync("DO0", true);

        result.ShouldBeFalse("persistent readback mismatch must fail");
        readbacks.ShouldBe(3, "verify must retry the configured number of times");
    }

    [Fact]
    public async Task SetOutputAsync_EventualReadback_ShouldSucceed()
    {
        _directions["DO0"] = "output";
        var readbacks = 0;
        var io = CreateIo(
            getLevel: _ => ++readbacks >= 2,                 // matches on 2nd readback
            setLevel: (_, _) => true);

        var result = await io.SetOutputAsync("DO0", true);

        result.ShouldBeTrue("a slow pin that settles within the retry window must succeed");
        readbacks.ShouldBe(2);
    }

    [Fact]
    public void GetLevel_UnknownPin_ShouldReturnNull()
    {
        var io = CreateIo();

        io.GetLevel("NOPE").ShouldBeNull();
    }

    [Fact]
    public void GetLevel_KnownPin_ShouldReturnState()
    {
        _states["DI0"] = true;
        var io = CreateIo();

        io.GetLevel("DI0").ShouldBe(true);
    }
}
