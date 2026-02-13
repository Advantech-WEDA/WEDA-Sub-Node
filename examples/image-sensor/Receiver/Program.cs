using System.Text.Json;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;
using Receiver;
using Receiver.Models;

// ===== Build with configuration =====
var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.Configure<ReceiverOptions>(builder.Configuration.GetSection("Receiver"));
builder.Services.Configure<WedaNodeOptions>(builder.Configuration.GetSection("WedaNode"));

var app = builder.Build();

var options = app.Services.GetRequiredService<IOptions<ReceiverOptions>>().Value;
var wedaNode = app.Services.GetRequiredService<IOptions<WedaNodeOptions>>().Value;
var imageSensorRoot = Path.GetFullPath(options.ImageSensorRoot);

// Read telemetry subject from ImageSensor's registration file
var registrationPath = Path.Combine(imageSensorRoot, ".weda", "subnode.registration.json");
if (!File.Exists(registrationPath))
{
    Console.WriteLine($"Registration file not found. Please run ImageSensor first to register.");
    Console.WriteLine($"Searched: {registrationPath}");
    return;
}

var regJson = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(registrationPath));
var subject = regJson.GetProperty("natsTopicAssignments").GetProperty("telemetryTopic").GetString()
    ?? throw new InvalidOperationException("telemetryTopic not found in registration file");

var natsUrl = $"nats://{wedaNode.Url}";
Console.WriteLine($"NATS Image Receiver");
Console.WriteLine($"  NATS: {natsUrl}");
Console.WriteLine($"  Subject: {subject}");
Console.WriteLine($"  HTTP: {builder.Configuration["Urls"]}");
Console.WriteLine();

// ===== State =====
var imageStore = new ImageStore(options.MaxImages);

// ===== NATS Subscriber =====
var natsOpts = NatsOpts.Default with
{
    Url = natsUrl,
    AuthOpts = new NatsAuthOpts { Username = wedaNode.Username, Password = wedaNode.Password }
};
var natsClient = new NatsClient(natsOpts);

var subscriberTask = Task.Run(async () =>
{
    Console.WriteLine($"Subscribing to {subject}...");

    await foreach (var msg in natsClient.SubscribeAsync<byte[]>(subject))
    {
        try
        {
            if (msg.Data == null || msg.Data.Length == 0) continue;

            var json = System.Text.Encoding.UTF8.GetString(msg.Data);
            var envelope = JsonSerializer.Deserialize<TelemetryEnvelope>(json);
            if (envelope?.Data?.Measures == null) continue;

            foreach (var measure in envelope.Data.Measures)
            {
                if (measure.Value is not JsonElement valueElement) continue;
                var base64Chunk = valueElement.GetString();
                if (string.IsNullOrEmpty(base64Chunk)) continue;

                var metadata = measure.Metadata;

                // Check if this is a chunked message
                if (metadata != null
                    && metadata.TryGetValue("transferId", out var transferIdEl)
                    && metadata.TryGetValue("chunkIndex", out var chunkIndexEl)
                    && metadata.TryGetValue("totalChunks", out var totalChunksEl))
                {
                    var imageId = transferIdEl.GetString()!;
                    var chunkIndex = chunkIndexEl.GetInt32();
                    var totalChunks = totalChunksEl.GetInt32();
                    uint? checksum = metadata.TryGetValue("crc32Checksum", out var checksumEl)
                        ? checksumEl.GetUInt32()
                        : null;

                    imageStore.AddChunk(imageId, chunkIndex, totalChunks, base64Chunk, measure.SensorId, checksum);
                }
                else
                {
                    // Non-chunked: complete image in one message
                    var imageId = Guid.NewGuid().ToString();
                    imageStore.AddComplete(imageId, base64Chunk, measure.SensorId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    }
});

// ===== HTTP Endpoints =====
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/latest", () =>
{
    var latest = imageStore.GetLatest();
    if (latest == null)
        return Results.Json(new { available = false });

    return Results.Json(new
    {
        available = true,
        latest.Base64,
        latest.SensorId,
        latest.Timestamp,
        latest.ContentType,
        latest.ImageId
    });
});

app.MapGet("/api/images", () =>
{
    var images = imageStore.GetAll();
    return Results.Json(images.Select(i => new
    {
        i.ImageId,
        i.SensorId,
        i.Timestamp,
        i.ContentType,
        size = i.Base64.Length
    }));
});

app.MapGet("/api/images/{id}", (string id) =>
{
    var image = imageStore.Get(id);
    if (image == null) return Results.NotFound();

    var bytes = Convert.FromBase64String(image.Base64);
    return Results.File(bytes, image.ContentType, $"{id}.png");
});

await app.RunAsync();
