using System.Text.Json;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Commands;

/// <summary>
/// Verifies CommandDispatcher's Step 0 (DTDL payload validation) boundary
/// behaviour: validator must run only when the IOptions flag is on, and a
/// validation miss must produce a Rejected/ValidationFailed response.
/// </summary>
public class CommandDispatcherStep0Tests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly CommandRegistry _registry;

    public CommandDispatcherStep0Tests()
    {
        _context = new MockApplicationContext
        {
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                DeviceId = "test-subnode-001",
                Manufacturer = "Test",
                Model = "MockSubNode",
                SwVersion = "1.0.0",
            },
        };
        _registry = new CommandRegistry();
        _registry.ScanAssembly(typeof(CommandRegistry).Assembly);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Step0_enabled_rejects_payload_missing_dtdl_required_field()
    {
        // outputs is marked [Required] on SetDigitalOutputParameters, which
        // surfaces as ConfigConstraint required:true in the emitted DTDL.
        // Omitting it must be caught by Step 0 before any handler runs.
        var options = Options.Create(new DtdlValidationOptions { Enabled = true });
        var dispatcher = new CommandDispatcher(_registry, _context, options);

        var message = BuildMessage("do.set", new { /* outputs intentionally missing */ });

        var result = await dispatcher.DispatchAsync(message);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Command.ValidationFailed");
        result.FirstError.Description.ShouldContain("required Field is missing");

        await _context.MockCloudService.Received(1).SendCommandResponseAsync(
            "test.resp",
            Arg.Is<CommandResponse>(r =>
                r.Data != null
                && r.Data.Status == CommandStatusCode.ValidationFailed));
    }

    [Fact]
    public async Task Step0_disabled_skips_dtdl_validator()
    {
        // With the flag off (default), the same invalid payload must not get
        // rejected by Step 0. The error — if any — should come from the
        // downstream DataAnnotation pass with a different message shape.
        var dispatcher = new CommandDispatcher(_registry, _context, dtdlValidationOptions: null);

        var message = BuildMessage("do.set", new { /* outputs intentionally missing */ });

        var result = await dispatcher.DispatchAsync(message);

        // Whatever fails downstream, it must NOT carry the WedaDtValidator's
        // signature error string — otherwise Step 0 ran when it should not have.
        if (result.IsError)
        {
            result.FirstError.Description.ShouldNotContain("required Field is missing");
        }
    }

    [Fact]
    public async Task Step0_enabled_passes_valid_payload_through()
    {
        // A payload that satisfies the DTDL contract must clear Step 0; any
        // subsequent error (handler not finding the device, etc.) must not be
        // a Step-0-shaped ValidationFailed.
        var options = Options.Create(new DtdlValidationOptions { Enabled = true });
        var dispatcher = new CommandDispatcher(_registry, _context, options);

        var message = BuildMessage("do.set", new
        {
            outputs = new[] { new { name = "do_0", state = true } },
        });

        var result = await dispatcher.DispatchAsync(message);

        if (result.IsError)
        {
            result.FirstError.Description.ShouldNotContain("required Field is missing");
        }
    }

    private static CommandMessage BuildMessage(string deviceCmd, object parameters)
    {
        var envelope = new
        {
            deviceCmd,
            timeout = 30,
            respTopic = "test.resp",
            parameters,
        };
        var json = JsonSerializer.Serialize(envelope);
        using var doc = JsonDocument.Parse(json);
        return new CommandMessage
        {
            Cmd = "deviceCmd",
            SeqId = 1,
            ReqSeqId = "req-1",
            Data = doc.RootElement.Clone(),
        };
    }
}
