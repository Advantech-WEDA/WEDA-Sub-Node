using System.Text.Json;

using ErrorOr;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Commands;

/// <summary>
/// Verifies the framework-owned ack can carry a handler-supplied payload via
/// <see cref="ICommandHandler{TCommand,TResult}.AckAsync"/>: a handler that overrides it enriches
/// the ack's resultData, a handler that does not ships a plain ack, and a throwing AckAsync must
/// never fail the ack.
/// </summary>
public class CommandDispatcherAckPayloadTests : IDisposable
{
    private readonly MockApplicationContext _context;

    public CommandDispatcherAckPayloadTests()
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
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Ack_carries_payload_supplied_by_handler_AckAsync()
    {
        var registry = new CommandRegistry();
        registry.Register<AckPayloadCommand, AckPayloadResult>("ack.payload", new PayloadAckHandler());
        var dispatcher = new CommandDispatcher(registry, _context);

        var responses = CaptureResponses();
        await dispatcher.DispatchAsync(BuildMessage("ack.payload"));

        var ack = responses.SingleOrDefault(r => r.Data!.MsgType == "ack");
        ack.ShouldNotBeNull();
        ack.Data!.ResultData.ShouldBe(PayloadAckHandler.Payload);
        // Framework still owns executedAt on that same ack.
        ack.Data.ExecutedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Ack_is_plain_when_handler_does_not_override_AckAsync()
    {
        var registry = new CommandRegistry();
        registry.Register<AckPayloadCommand, AckPayloadResult>("ack.payload", new PlainAckHandler());
        var dispatcher = new CommandDispatcher(registry, _context);

        var responses = CaptureResponses();
        await dispatcher.DispatchAsync(BuildMessage("ack.payload"));

        var ack = responses.SingleOrDefault(r => r.Data!.MsgType == "ack");
        ack.ShouldNotBeNull();
        ack.Data!.ResultData.ShouldBeNull();
    }

    [Fact]
    public async Task Ack_is_still_sent_when_AckAsync_throws()
    {
        var registry = new CommandRegistry();
        registry.Register<AckPayloadCommand, AckPayloadResult>("ack.payload", new ThrowingAckHandler());
        var dispatcher = new CommandDispatcher(registry, _context);

        var responses = CaptureResponses();
        var result = await dispatcher.DispatchAsync(BuildMessage("ack.payload"));

        // The ack goes out anyway, without a payload; the failure to compute it must not
        // surface as an error nor block the handler body.
        var ack = responses.SingleOrDefault(r => r.Data!.MsgType == "ack");
        ack.ShouldNotBeNull();
        ack.Data!.ResultData.ShouldBeNull();
        ack.Data.ExecutedAt.ShouldNotBeNull();
        result.IsError.ShouldBeFalse();
    }

    private List<CommandResponse> CaptureResponses()
    {
        var responses = new List<CommandResponse>();
        _context.MockCloudService
            .SendCommandResponseAsync(Arg.Any<string>(), Arg.Do<CommandResponse>(responses.Add), Arg.Any<CancellationToken>())
            .Returns(true);
        return responses;
    }

    private static CommandMessage BuildMessage(string deviceCmd)
    {
        var envelope = new
        {
            deviceCmd,
            timeout = 30,
            respTopic = "test.resp",
            parameters = new { },
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

[DeviceCmd("ack.payload")]
public class AckPayloadCommand : CommandData<object>
{
}

public class AckPayloadResult : IResult
{
    public int Status => CommandStatusCode.Success;
    public string Message => "ok";
    public object? ResultData => null;
}

/// <summary>Handler that enriches the ack with a payload.</summary>
public class PayloadAckHandler : ICommandHandler<AckPayloadCommand, AckPayloadResult>
{
    public static readonly object Payload = new { estimatedSamples = 42, estimatedBatches = 3 };

    public Task<ErrorOr<AckPayloadResult>> HandleAsync(
        AckPayloadCommand command, IWedaApplicationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorOr<AckPayloadResult>>(new AckPayloadResult());

    public Task<object?> AckAsync(
        AckPayloadCommand command, IWedaApplicationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(Payload);
}

/// <summary>Handler that leaves AckAsync at its default (plain ack).</summary>
public class PlainAckHandler : ICommandHandler<AckPayloadCommand, AckPayloadResult>
{
    public Task<ErrorOr<AckPayloadResult>> HandleAsync(
        AckPayloadCommand command, IWedaApplicationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorOr<AckPayloadResult>>(new AckPayloadResult());
}

/// <summary>Handler whose AckAsync throws; the ack must still be sent, without a payload.</summary>
public class ThrowingAckHandler : ICommandHandler<AckPayloadCommand, AckPayloadResult>
{
    public Task<ErrorOr<AckPayloadResult>> HandleAsync(
        AckPayloadCommand command, IWedaApplicationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorOr<AckPayloadResult>>(new AckPayloadResult());

    public Task<object?> AckAsync(
        AckPayloadCommand command, IWedaApplicationContext context, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("ack payload computation failed");
}
