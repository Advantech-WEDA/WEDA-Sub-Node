using System.Text.Json;
using System.Text.Json.Nodes;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using NATS.Net;

using Shouldly;

using Weda.Dtdl.Validation;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.TestBase.Builders;

using Xunit;

namespace Weda.SubNode.Integration.Tests.Cloud;

/// <summary>
/// End-to-end verification that DTDL Interfaces emitted by SubNode survive the
/// full upload pipeline — descriptor build, mapping to DTO, NATS request/reply,
/// and JSON round-trip — and arrive at the cloud-side "Shadow" stand-in in a
/// shape that <see cref="WedaDtdlValidator"/> accepts without throwing.
/// </summary>
/// <remarks>
/// The validator's constructor runs the full <c>WedaDtdlParser</c> + the
/// <c>ConfigConstraintValidator</c> screening pass, so a successful
/// construction proves both DTDL v3 well-formedness AND ConfigConstraint
/// extension correctness for the inbound payload.
/// </remarks>
public class CapabilityUploadDtdlE2ETests : IAsyncLifetime
{
    private const string UploadConfigSubject = "eco1j.weda.dm.cfg.update.req";

    private readonly IContainer _natsContainer = new ContainerBuilder()
        .WithImage("nats:2-alpine")
        .WithPortBinding(4222, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(4222))
        .Build();

    private string NatsUrl => $"nats://localhost:{_natsContainer.GetMappedPublicPort(4222)}";

    public async Task InitializeAsync() => await _natsContainer.StartAsync();
    public async Task DisposeAsync() => await _natsContainer.DisposeAsync();

    [Fact]
    public async Task Shadow_receives_dtdl_interfaces_parseable_by_validator()
    {
        // Two clients sharing the NATS container — one for the cloud-side
        // subscriber, one as the SubNode-side DeviceAgentClient.
        await using var shadowClient = new NatsClient(NatsUrl);
        await using var subNodeClient = new NatsClient(NatsUrl);

        // Cloud-side: subscribe to the upload subject, capture the request,
        // reply with an "accepted" response. We bypass MockDeviceAgent's
        // service-context wrapper and use a plain subject subscription so
        // the test owns the capture state.
        ConfigurationUploadRequest? captured = null;
        var captureReady = new TaskCompletionSource();
        var subscriberCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var msg in shadowClient.SubscribeAsync<ConfigurationUploadRequest>(
                UploadConfigSubject, cancellationToken: subscriberCts.Token))
            {
                captured = msg.Data;
                await msg.ReplyAsync(new ConfigurationUploadResponse
                {
                    ReqSeqId = msg.Data?.ReqSeqId ?? string.Empty,
                    Code = 0,
                    Message = "OK",
                    Data = new ConfigurationUploadResponseData { ConfigurationStatus = "accepted" },
                }, cancellationToken: subscriberCts.Token);
                captureReady.TrySetResult();
                break;
            }
        });

        // Give NATS a tick to register the subscription before we publish.
        await Task.Delay(200);

        // SubNode side: build a minimum-viable DeviceConfigurations + a real
        // CommandRegistry scanned from Weda.SubNode.Core. DeviceAgentClient
        // pulls Transform / DspFilter descriptors from the static factories
        // and command descriptors from the registry.
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);

        var configurations = new DeviceConfigurations
        {
            ["test-device"] = new DeviceConfigurationBuilder()
                .WithDeviceId("test-device-001")
                .WithDeviceName("E2E Device")
                .Build(),
        };

        var deviceAgentClient = new DeviceAgentClient(subNodeClient, logger: null, commandRegistry: registry);

        var response = await deviceAgentClient.UploadDeviceConfigurationAsync(configurations);

        response.Data!.ConfigurationStatus.ShouldBe("accepted");

        // Wait for the capture handler to finish (it has already replied;
        // this just synchronises before assertions).
        await captureReady.Task;
        subscriberCts.Cancel();
        await Task.WhenAny(subscriberTask, Task.Delay(2000));

        captured.ShouldNotBeNull();
        var capabilities = captured!.Data!.DeviceCapabilities.Capabilities;

        // Every descriptor's emitted DTDL must construct a WedaDtdlValidator
        // without throwing. That's the binary "shadow accepted the DTDL" check.
        capabilities.Transforms.ShouldNotBeEmpty();
        foreach (var transform in capabilities.Transforms)
        {
            ShouldConstructValidator(transform.ParameterSchema,
                $"transform '{transform.TypeName}'");
        }

        capabilities.DspFilters.ShouldNotBeEmpty();
        foreach (var filter in capabilities.DspFilters)
        {
            ShouldConstructValidator(filter.ParameterSchema,
                $"dspFilter '{filter.TypeName}'");
        }

        capabilities.Commands.ShouldNotBeEmpty();
        foreach (var command in capabilities.Commands)
        {
            ShouldConstructValidator(command.ParameterSchema,
                $"command '{command.Name}' parameter");
            ShouldConstructValidator(command.ResponseSchema,
                $"command '{command.Name}' response");
        }
    }

    private static void ShouldConstructValidator(JsonObject schema, string label)
    {
        var json = schema.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        try
        {
            _ = new WedaDtdlValidator(json);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"DTDL for {label} failed validator construction: {ex.Message}\n\nPayload:\n{json}");
        }
    }
}
