using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using NATS.Net;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Cloud;

using Xunit;

namespace Weda.SubNode.Integration.Tests.Cloud;

/// <summary>
/// End-to-end verification that WedaCloudService surfaces NATS reconnection
/// as the ConnectionRestored event (US-47107 AC-5): a SubNode that reconnects
/// after a broker outage must republish its reported configuration, which
/// depends on this event actually firing on a real reconnect.
/// </summary>
public class ConnectionRestoredE2ETests : IAsyncLifetime
{
    // A fixed host port is required: a random binding would be re-assigned when
    // the container restarts, and the NATS client would reconnect into the void.
    private readonly int _hostPort = GetFreeTcpPort();
    private readonly IContainer _natsContainer;

    public ConnectionRestoredE2ETests()
    {
        _natsContainer = new ContainerBuilder()
            .WithImage("nats:2-alpine")
            .WithPortBinding(_hostPort, 4222)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(4222))
            .Build();
    }

    // Resolve the host from the container, never hardcode "localhost". When the build agent is
    // itself a container sharing the Docker socket (the CI pool), published ports land on the
    // Docker host, not inside the agent — "localhost" then reaches nothing and the test fails
    // with "can not connect uris". It passes on a developer machine only because the test
    // process and the daemon happen to share a host. Hostname resolves correctly in both.
    private string NatsUrl => $"nats://{_natsContainer.Hostname}:{_hostPort}";

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task InitializeAsync() => await _natsContainer.StartAsync();
    public async Task DisposeAsync() => await _natsContainer.DisposeAsync();

    [Fact]
    public async Task ConnectionRestored_ShouldFire_WhenNatsConnectionDropsAndRecovers()
    {
        // Arrange - responder standing in for the WedaNode service ping
        await using var responder = new NatsClient(NatsUrl);
        var responderReady = new TaskCompletionSource();
        _ = Task.Run(async () =>
        {
            var subscription = responder.SubscribeAsync<string>("$SRV.PING");
            responderReady.TrySetResult();
            await foreach (var msg in subscription)
            {
                await msg.ReplyAsync(new NatsServicePingResponse
                {
                    Name = "test-wedanode",
                    Id = "test",
                    Version = "1.0.0"
                });
            }
        });
        await responderReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await responder.PingAsync(); // ensure the subscription reached the server

        var client = new NatsClient(NatsUrl);
        var cloudService = new WedaCloudService(
            client,
            Substitute.For<IDeviceAgentClient>(),
            Substitute.For<ITelemetryClient>(),
            Substitute.For<IDeviceRegistrationStorage>());

        var restored = new TaskCompletionSource();
        cloudService.ConnectionRestored += () =>
        {
            restored.TrySetResult();
            return Task.CompletedTask;
        };

        var connected = await cloudService.ConnectAsync();
        connected.ShouldBeTrue("initial connection must succeed before testing reconnect");
        restored.Task.IsCompleted.ShouldBeFalse("initial connection must not raise ConnectionRestored");

        // Act - drop the broker and bring it back; the NATS client auto-reconnects
        await _natsContainer.StopAsync();
        await _natsContainer.StartAsync();

        // Assert
        var fired = await Task.WhenAny(restored.Task, Task.Delay(TimeSpan.FromSeconds(60)));
        fired.ShouldBe(restored.Task, "ConnectionRestored should fire after the broker comes back");
    }
}
