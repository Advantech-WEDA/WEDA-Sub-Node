using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Cloud;
using Weda.SubNode.Host.Context;

// Manual, runnable batch-report demo in the SubNode-template style (manual WedaApplicationContext +
// MockCloudService), NOT the wedabuilder host. It seeds recordings, initialises the SubNodeManager
// so it subscribes to commands on the mock cloud, injects a report.historical command through the
// real dispatch path, and lets the mock cloud log every outbound message (subject + payload) to a
// sink file. It then prints that file so you can inspect the ack (with the estimated download
// volume), progress, batch telemetry, and final result end to end.
//
// Run:  dotnet run --project tests/Weda.SubNode.BatchReportDemo

var sinkPath = Path.Combine(AppContext.BaseDirectory, "batchreport-demo-sink.jsonl");
if (File.Exists(sinkPath))
{
    File.Delete(sinkPath);
}

var mockCloud = new MockCloudService { SinkFilePath = sinkPath };

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.SingleLine = true)
    .SetMinimumLevel(LogLevel.Information));

var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

using var context = new WedaApplicationContext(options =>
{
    options.Configuration = configuration;
    options.CloudService = mockCloud;
    options.LoggerFactory = loggerFactory;
    options.AutoLoadDtdl = false;
    options.UseCache = false;
    options.DeviceOptions = new DeviceOptions
    {
        EnableCommands = true,
        EnableTelemetry = true,
        EnableRecording = true,
    };
});

if (context.RecordingService is null)
{
    Console.Error.WriteLine("RecordingService not available; cannot run the demo.");
    return 1;
}

// Seed two sensors' history so the report has something to send.
var now = DateTimeOffset.UtcNow;
var start = now.AddMinutes(-5);
await SeedSensor(context.RecordingService, "sensor-a", start, count: 10, intervalMs: 1000);
await SeedSensor(context.RecordingService, "sensor-b", start, count: 5, intervalMs: 1000);
await context.RecordingService.FlushAsync();

// Initialise the SubNodeManager directly (connect, register, subscribe to commands). This is the
// Step-1 path SubNode.InitializeAsync delegates to; we skip its device-connection steps because
// batch report reads from the RecordingService, not from a live device.
var initialized = await context.SubNodeManager.InitializeAsync();
Console.WriteLine($"\nSubNodeManager initialized: {initialized}; SubNodeId={context.SubNodeInfo.Id}\n");
if (!initialized)
{
    return 1;
}

// Build a report.historical command envelope covering the seeded window and inject it.
var envelope = new
{
    deviceCmd = "report.historical",
    timeout = 60,
    respTopic = "mock.cmd.resp",
    parameters = new
    {
        timeRange = new
        {
            startTime = start.ToUnixTimeMilliseconds(),
            endTime = now.ToUnixTimeMilliseconds(),
        },
        maxBatchesPerMessage = 1,
        maxBatchSize = 4,
        transmissionRateLimit = 0,
    },
};
using var doc = JsonDocument.Parse(JsonSerializer.Serialize(envelope));

var injected = await mockCloud.InjectCommandAsync(
    context.SubNodeInfo.Id ?? "mock-device", doc.RootElement, seqId: 100, reqSeqId: "req-100");
Console.WriteLine($"Command injected: {injected}\n");

// Print the sink file: one JSON line per outbound message, pretty-printed by msgType/subject.
Console.WriteLine($"Sink file: {sinkPath}\n");
Console.WriteLine("===== Outbound messages (subject :: msgType) =====\n");

var pretty = new JsonSerializerOptions { WriteIndented = true };
foreach (var raw in await File.ReadAllLinesAsync(sinkPath))
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        continue;
    }

    var line = JsonDocument.Parse(raw).RootElement;
    var subject = line.GetProperty("subject").GetString();
    var payload = line.GetProperty("payload");

    var msgType = payload.TryGetProperty("data", out var data)
        && data.TryGetProperty("msgType", out var mt)
        ? mt.GetString()
        : "telemetry";

    Console.WriteLine($"--- {subject} :: {msgType} ---");
    Console.WriteLine(JsonSerializer.Serialize(payload, pretty));
    Console.WriteLine();
}

return 0;

static async Task SeedSensor(
    IRecordingService recordingService, string sensorId, DateTimeOffset start, int count, int intervalMs)
{
    for (var i = 0; i < count; i++)
    {
        var ts = start.AddMilliseconds(i * intervalMs).ToUnixTimeMilliseconds();
        await recordingService.RecordAsync(sensorId, intervalMs, SchemaType.Double, ts, (double)i);
    }
}
