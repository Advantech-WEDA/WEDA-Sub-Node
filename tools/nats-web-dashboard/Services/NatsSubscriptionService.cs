using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NatsWebDashboard.Configuration;
using NatsWebDashboard.Hubs;

namespace NatsWebDashboard.Services;

/// <summary>
/// Background service that subscribes to a NATS subject and forwards
/// every received message to all connected SignalR clients.
/// </summary>
public sealed class NatsSubscriptionService : BackgroundService
{
    private readonly DashboardOptions _options;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<NatsSubscriptionService> _logger;

    public NatsSubscriptionService(
        IOptions<DashboardOptions> options,
        IHubContext<TelemetryHub> hub,
        ILogger<NatsSubscriptionService> logger)
    {
        _options = options.Value;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var natsSettings = _options.Nats;
        var natsOpts = new NatsOpts
        {
            Url = natsSettings.Url,
            AuthOpts = natsSettings.BuildAuthOpts()
        };

        await using var connection = new NatsConnection(natsOpts);
        await connection.ConnectAsync();
        _logger.LogInformation("Connected to NATS: {Url}", natsSettings.Url);
        _logger.LogInformation("Subscribing to: {Subject}", _options.Subject);

        await foreach (var msg in connection.SubscribeAsync<byte[]>(_options.Subject, cancellationToken: stoppingToken))
        {
            var payload = msg.Data is not null ? Encoding.UTF8.GetString(msg.Data) : "";

            await _hub.Clients.All.SendAsync(
                "NatsMessage",
                msg.Subject,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload,
                stoppingToken);
        }
    }
}
