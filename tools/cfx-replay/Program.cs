using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

using MQTTnet;
using MQTTnet.Client;

using Weda.SubNode.Core.Protocols.Cfx;

namespace CfxReplay;

/// <summary>
/// Replays captured CFX message payloads onto an MQTT broker, standing in for a bridged CFX line.
/// </summary>
/// <remarks>
/// <para>
/// Publishes GZip-compressed envelope JSON, exactly as a RabbitMQ AMQP-to-MQTT bridge delivers it.
/// By default the envelope's <c>TimeStamp</c> is restamped to publish time so a replay lands as
/// live data; set <c>CFX_RESTAMP=false</c> to publish the captures byte-for-byte instead, so a
/// subscriber under test sees real wire bytes rather than re-encoded approximations. See
/// <see cref="BuildPayloadBytes"/> for the trade-off.
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

    /// <summary>Envelope property carrying the publisher's timestamp.</summary>
    private const string TimeStampProperty = "TimeStamp";

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
        var restamp = bool.Parse(Env("CFX_RESTAMP", "true"));

        var payloads = LoadPayloads(payloadDirectory);
        if (payloads.Count == 0)
        {
            Console.Error.WriteLine($"No .gz payloads found in '{Path.GetFullPath(payloadDirectory)}'.");
            return 1;
        }

        Log($"Loaded {payloads.Count} captured CFX payloads from {Path.GetFullPath(payloadDirectory)}");
        Log(restamp
            ? "Restamping envelope TimeStamp to publish time (CFX_RESTAMP=true)"
            : "Publishing captured bytes verbatim; measures will land at their original capture time "
              + "(CFX_RESTAMP=false)");

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
                    var bytes = BuildPayloadBytes(payload, restamp);

                    await client.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(bytes)
                            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build(),
                        cts.Token);

                    Log($"published {bytes.Length,5} B -> {topic}");

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
    /// Produces the bytes to publish for a capture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A capture carries the <c>TimeStamp</c> the endpoint stamped when it was recorded, and the
    /// SubNode's parser reports that as the measure's time — correct against a live line, but it
    /// means replayed data lands however old the captures are, where no recent-window query or
    /// dashboard will show it. Restamping to publish time makes a replay look live.
    /// </para>
    /// <para>
    /// Restamping costs the byte-for-byte fidelity of the verbatim path: the envelope is re-serialized
    /// and re-compressed, so whitespace and compressed length change even though every value is
    /// preserved. Set <c>CFX_RESTAMP=false</c> to publish the original bytes when exercising the
    /// decoder against real wire bytes matters more than timestamp freshness.
    /// </para>
    /// </remarks>
    private static byte[] BuildPayloadBytes(Payload payload, bool restamp)
    {
        if (!restamp)
        {
            return payload.Bytes;
        }

        var envelope = JsonNode.Parse(payload.Json)?.AsObject();
        if (envelope is null)
        {
            // Parsed cleanly at load time, so this cannot normally happen; fall back rather than
            // dropping the message from the replay.
            return payload.Bytes;
        }

        // Only the envelope's own TimeStamp drives the measure time. Body-level timestamps are left
        // as captured: they are process data, not the reporting clock.
        envelope[TimeStampProperty] = DateTimeOffset.Now.ToString("O");

        return Gzip(envelope.ToJsonString());
    }

    private static byte[] Gzip(string json)
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
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

            payloads[envelope.Value.MessageName] = new Payload(bytes, decoded.Value, envelope.Value.Source);
        }

        return payloads;
    }

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static void Log(string message) =>
        Console.WriteLine($"[cfx-replay] {message}");

    private sealed record Payload(byte[] Bytes, string Json, string? Source);
}
