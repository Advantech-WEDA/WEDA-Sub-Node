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
/// shape that <see cref="WedaDtValidator"/> accepts without throwing.
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
        var data = captured!.Data!;

        // Catalog references are thin (name + dtmi); definitions live in dtdl[].
        data.DeviceCapabilities.Transforms.ShouldNotBeEmpty();
        data.DeviceCapabilities.DspFilters.ShouldNotBeEmpty();
        data.DeviceCapabilities.Commands.ShouldNotBeEmpty();

        // Every catalog dtmi must resolve to an entry in dtdl[].
        var refIds = data.RefModels
            .Select(i => i["@id"]?.GetValue<string>())
            .Where(id => id is not null)
            .ToHashSet();
        foreach (var r in data.DeviceCapabilities.Transforms) refIds.ShouldContain(r.Dtmi);
        foreach (var r in data.DeviceCapabilities.DspFilters) refIds.ShouldContain(r.Dtmi);
        foreach (var r in data.DeviceCapabilities.Commands)   refIds.ShouldContain(r.Dtmi);

        // The wrapper Interface (data.Dtdl) plus every refModels[] entry must each
        // construct a WedaDtValidator without throwing — that's the binary
        // "shadow accepted the DTDL" check. We feed each Interface plus the
        // transitive closure of its `extends` bases so the validator can resolve
        // inherited Contents.
        data.Dtdl.ShouldNotBeEmpty();
        data.RefModels.ShouldNotBeEmpty();

        var byId = data.RefModels
            .Where(i => i["@id"]?.GetValue<string>() is not null)
            .ToDictionary(i => i["@id"]!.GetValue<string>(), i => i);
        
        ShouldConstructValidator(data.Dtdl, byId, "wrapper Interface");

        foreach (var iface in data.RefModels)
        {
            var id = iface["@id"]?.GetValue<string>() ?? "<no-id>";
            ShouldConstructValidator(iface, byId, $"refModel '{id}'");
        }

        // refModelsMap is a lossless partition of refModels: commands = command
        // Interfaces, configs = everything else, configs ∪ commands (by @id) ==
        // refModels. The flat list is retained for DTDL parsing; the map lets
        // consumers take a category slice without inferring from @id / extends.
        var map = data.RefModelsMap;
        map.ShouldNotBeNull();

        map.Configs.ShouldNotBeEmpty();
        map.Commands.ShouldNotBeEmpty();
        map.Commands.ShouldAllBe(i => i["@id"]!.GetValue<string>().Contains(":command:"));
        map.Configs.ShouldAllBe(i => !i["@id"]!.GetValue<string>().Contains(":command:"));

        var mapIds = map.Configs.Concat(map.Commands)
            .Select(i => i["@id"]!.GetValue<string>())
            .ToHashSet();
        var flatIds = data.RefModels
            .Select(i => i["@id"]!.GetValue<string>())
            .ToHashSet();
        mapIds.SetEquals(flatIds).ShouldBeTrue();
    }

    private static void ShouldConstructValidator(
        JsonObject leaf, IReadOnlyDictionary<string, JsonObject> byId, string label)
    {
        var opts = new JsonSerializerOptions { WriteIndented = false };
        var docs = new List<JsonObject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectClosure(leaf, byId, docs, seen);

        var jsons = docs.Select(d => d.ToJsonString(opts)).ToList();
        var parsedDocs = jsons.Select(j => JsonDocument.Parse(j)).ToList();
        try
        {
            try
            {
                _ = new WedaDtValidator(parsedDocs.Select(d => d.RootElement));
            }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"DTDL for {label} failed validator construction: {ex.Message}\n\nPayload:\n{jsons[0]}");
            }
        }
        finally
        {
            foreach (var doc in parsedDocs) doc.Dispose();
        }
    }

    private static void CollectClosure(
        JsonObject node,
        IReadOnlyDictionary<string, JsonObject> byId,
        List<JsonObject> docs,
        HashSet<string> seen)
    {
        var id = node["@id"]?.GetValue<string>();
        if (id is null || !seen.Add(id)) return;
        docs.Add(node);

        // `extends` is emitted as a string when there's a single base, and as a
        // JsonArray when there are multiple — handle both shapes.
        switch (node["extends"])
        {
            case JsonValue v when v.TryGetValue<string>(out var single) && byId.TryGetValue(single, out var b1):
                CollectClosure(b1, byId, docs, seen);
                break;
            case JsonArray arr:
                foreach (var entry in arr)
                {
                    if (entry is JsonValue jv && jv.TryGetValue<string>(out var dtmi)
                        && byId.TryGetValue(dtmi, out var b2))
                    {
                        CollectClosure(b2, byId, docs, seen);
                    }
                }
                break;
        }
    }
}
