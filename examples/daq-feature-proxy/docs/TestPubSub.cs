using System.Text.Json;
using System.Text.Json.Nodes;
using NATS.Net;

namespace nats_test;

public static class TestPubSub
{
    private const string NatsUrl = "nats://advantech_nats:3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48@localhost:4224";

    public static async Task QuerySubnodeCap(
        IEnumerable<string>? deviceIds = null,
        string? status = null,
        int timeoutSeconds = 5)
    {
        await using var client = new NatsClient(NatsUrl);

        const string subject = "eco1j.weda.dm.subnode.cap.query.req";

        var data = new JsonObject();
        var ids = deviceIds?.ToArray();
        if (ids is { Length: > 0 })
            data["deviceIds"] = JsonSerializer.SerializeToNode(ids);
        if (!string.IsNullOrEmpty(status))
            data["status"] = status;

        var requestObj = new JsonObject
        {
            ["reqSeqId"] = "query-subnode-caps",
            ["data"] = data,
        };
        var payload = requestObj.ToJsonString();

        Console.WriteLine($"Sending request to: {subject}");
        Console.WriteLine($"Payload: {payload}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var reply = await client.RequestAsync<string, string>(subject, payload, cancellationToken: cts.Token);

            if (reply.Data is null)
            {
                Console.WriteLine("Received empty reply.");
                return;
            }

            Console.WriteLine($"Reply received: {reply.Data}");

            var node = JsonNode.Parse(reply.Data);
            if (node is not null)
            {
                Console.WriteLine($"Parsed JSON:\n{node.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Request timed out after {timeoutSeconds} seconds. No responder available.");
        }
    }
}