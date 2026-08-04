using MQTTnet;
using MQTTnet.Client;

using Weda.SubNode.Core.Protocols.Cfx;

namespace CfxReplay;

/// <summary>
/// Replays captured CFX message payloads onto an MQTT broker, standing in for a bridged CFX line.
/// </summary>
/// <remarks>
/// <para>
/// Publishes the captured payloads byte-for-byte — GZip-compressed envelope JSON, exactly as a
/// RabbitMQ AMQP-to-MQTT bridge delivers them — so a subscriber under test sees real wire bytes
/// rather than re-encoded approximations.
/// </para>
/// <para>
/// Each topic is derived from the envelope's own <c>Source</c> handle and <c>MessageName</c>, so the
/// replayed topics match the documented ones without a hard-coded table, and messages fan out across
/// the several endpoint handles present in the captures.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Nominal order of a production flow. CFX does not define emission order, so this is a
    /// plausible sequence for demonstration only — it is not a contract a consumer may rely on.
    /// </summary>
    private static readonly string[] ReplayOrder = [.. CfxMessageCatalog.SupportedMessageNames];

    private static async Task<int> Main()
    {
        var host = Env("CFX_BROKER_HOST", "localhost");
        var port = int.Parse(Env("CFX_BROKER_PORT", "1883"));
        var username = Environment.GetEnvironmentVariable("CFX_BROKER_USERNAME");
        var password = Environment.GetEnvironmentVariable("CFX_BROKER_PASSWORD");
        var payloadDirectory = Env("CFX_PAYLOAD_DIR", "payloads");
        var intervalMs = int.Parse(Env("CFX_INTERVAL_MS", "1000"));
        var cycleDelayMs = int.Parse(Env("CFX_CYCLE_DELAY_MS", "5000"));
        var loop = bool.Parse(Env("CFX_LOOP", "true"));

        var payloads = LoadPayloads(payloadDirectory);
        if (payloads.Count == 0)
        {
            Console.Error.WriteLine($"No .gz payloads found in '{Path.GetFullPath(payloadDirectory)}'.");
            return 1;
        }

        Log($"Loaded {payloads.Count} captured CFX payloads from {Path.GetFullPath(payloadDirectory)}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"cfx-replay-{Guid.NewGuid():N}")
            .WithCleanSession();

        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(username, password);
        }

        var options = optionsBuilder.Build();

        await ConnectWithRetryAsync(client, options, host, port, cts.Token);

        var cycle = 0;
        try
        {
            do
            {
                cycle++;
                Log($"--- cycle {cycle} ---");

                foreach (var messageName in ReplayOrder)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    if (!payloads.TryGetValue(messageName, out var payload))
                    {
                        continue;
                    }

                    var topic = TopicFor(messageName, payload);

                    await client.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(payload.Bytes)
                            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build(),
                        cts.Token);

                    Log($"published {payload.Bytes.Length,5} B -> {topic}");

                    await Task.Delay(intervalMs, cts.Token);
                }

                if (loop)
                {
                    await Task.Delay(cycleDelayMs, cts.Token);
                }
            }
            while (loop && !cts.Token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled, shutting down.");
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync();
            }
        }

        Log("Done.");
        return 0;
    }

    /// <summary>
    /// Connects to the broker, retrying so the replayer tolerates being started before it.
    /// </summary>
    private static async Task ConnectWithRetryAsync(
        IMqttClient client,
        MqttClientOptions options,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await client.ConnectAsync(options, cancellationToken);
                Log($"Connected to MQTT broker {host}:{port}");
                return;
            }
            catch (Exception ex) when (attempt < 30 && !cancellationToken.IsCancellationRequested)
            {
                Log($"Broker {host}:{port} not ready (attempt {attempt}): {ex.Message}. Retrying in 2s.");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Rebuilds the topic a capture was originally published on, from the envelope it contains.
    /// </summary>
    private static string TopicFor(string messageName, Payload payload)
    {
        var relativePath = CfxMessageCatalog.ToRelativeTopicPath(messageName);

        return string.IsNullOrWhiteSpace(payload.Source)
            ? $"unknown/endpoint/0000/{CfxTopic.DefaultRoot}/{relativePath}"
            : $"{CfxTopic.ToTopicPrefix(payload.Source)}/{CfxTopic.DefaultRoot}/{relativePath}";
    }

    private static Dictionary<string, Payload> LoadPayloads(string directory)
    {
        var payloads = new Dictionary<string, Payload>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
        {
            return payloads;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.gz"))
        {
            var bytes = File.ReadAllBytes(file);

            // Decode only to learn the envelope's identity; the original bytes are what gets published.
            var decoded = CfxPayloadCodec.Decode(bytes);
            if (decoded.IsError)
            {
                Console.Error.WriteLine(
                    $"Skipping {Path.GetFileName(file)}: {decoded.FirstError.Code} {decoded.FirstError.Description}");
                continue;
            }

            var envelope = CfxEnvelopeReader.Read(decoded.Value);
            if (envelope.IsError)
            {
                Console.Error.WriteLine(
                    $"Skipping {Path.GetFileName(file)}: {envelope.FirstError.Code} {envelope.FirstError.Description}");
                continue;
            }

            payloads[envelope.Value.MessageName] = new Payload(bytes, envelope.Value.Source);
        }

        return payloads;
    }

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static void Log(string message) =>
        Console.WriteLine($"[cfx-replay] {message}");

    private sealed record Payload(byte[] Bytes, string? Source);
}
