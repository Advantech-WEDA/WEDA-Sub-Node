using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Cloud;
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;

using Xunit;
using Xunit.Abstractions;

namespace Weda.SubNode.Core.Tests.Commands;

/// <summary>
/// End-to-end batch report exercised through the manual-context style used by the SubNode template
/// (new WedaApplicationContext + MockCloudService + SubNode), rather than the wedabuilder host.
/// </summary>
/// <remarks>
/// This drives the real pipeline: a real RecordingService is seeded with history, the SubNode is
/// initialised so SubNodeManager subscribes to commands on the mock cloud, then a report.historical
/// command is injected. The mock cloud writes every outbound message (subject + payload) to a sink
/// file; the test reads that file back and asserts the framework-owned ack carries BatchReport's
/// estimated download volume, followed by batch telemetry and the final result.
///
/// The RecordingService storage directory is a hardcoded const, so this test cannot run in parallel
/// with anything else that touches recordings: it is placed in a dedicated non-parallel collection
/// and wipes the storage directory before and after.
/// </remarks>
[Collection("RecordingStorage")]
public class BatchReportManualContextHarnessTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _storageDir;

    public BatchReportManualContextHarnessTests(ITestOutputHelper output)
    {
        _output = output;
        _storageDir = PathHelperResolve();
        WipeStorage();
    }

    public void Dispose() => WipeStorage();

    [Fact]
    public async Task Injected_report_historical_writes_ack_with_estimate_then_batches_and_result()
    {
        var sinkPath = Path.Combine(Path.GetTempPath(), $"batchreport-sink-{Guid.NewGuid():N}.jsonl");
        var mockCloud = new MockCloudService { SinkFilePath = sinkPath };

        // Minimal in-memory configuration; recording enabled so the context builds a real
        // RecordingService (writing to its fixed storage directory).
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        using var context = new WedaApplicationContext(options =>
        {
            options.Configuration = configuration;
            options.CloudService = mockCloud;
            options.LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b =>
                b.AddProvider(new XunitLoggerProvider(_output)).SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
            options.AutoLoadDtdl = false;
            options.UseCache = false;
            options.DeviceOptions = new DeviceOptions
            {
                EnableCommands = true,
                EnableTelemetry = true,
                EnableRecording = true,
            };
        });

        context.RecordingService.ShouldNotBeNull();

        // Seed two sensors' history into the real recording service, then flush to disk.
        var now = DateTimeOffset.UtcNow;
        var start = now.AddMinutes(-5);
        await SeedSensor(context.RecordingService, "sensor-a", start, count: 10, intervalMs: 1000);
        await SeedSensor(context.RecordingService, "sensor-b", start, count: 5, intervalMs: 1000);
        await context.RecordingService.FlushAsync();

        // Initialise the SubNodeManager directly: this is the Step-1 path SubNode.InitializeAsync
        // delegates to (connect, register, subscribe to commands). We skip SubNode's device-connection
        // steps because batch report reads from the RecordingService, not from a live device — and
        // SubNode.InitializeAsync refuses to run with zero devices registered.
        var initialized = await context.SubNodeManager.InitializeAsync();
        _output.WriteLine($"SubNodeManager initialized: {initialized}; SubNodeId={context.SubNodeInfo.Id}");
        initialized.ShouldBeTrue();

        // Build a report.historical envelope covering the seeded window and inject it as a command.
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
        injected.ShouldBeTrue();

        // Read the sink back: one JSON line per outbound message.
        File.Exists(sinkPath).ShouldBeTrue();
        var lines = (await File.ReadAllLinesAsync(sinkPath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToList();

        foreach (var l in lines)
        {
            _output.WriteLine($"{l.GetProperty("subject").GetString()} :: {l.GetProperty("payload")}");
        }

        // The ack (msgType=ack) must carry the estimated download volume in resultData.
        var ack = lines.Single(l => MsgType(l) == "ack");

        ack.GetProperty("subject").GetString().ShouldBe("mock.cmd.resp");
        var ackData = ack.GetProperty("payload").GetProperty("data");
        ackData.GetProperty("executedAt").GetInt64().ShouldBeGreaterThan(0);
        var estimate = ackData.GetProperty("resultData");
        // The estimate is derived from time-range ÷ interval up front (before reading), so it is an
        // upper-bound projection, deliberately NOT the actual seeded count — that is the whole point
        // of shipping it on the ack. Just assert it is a positive, plausible projection.
        estimate.GetProperty("estimatedSamples").GetInt32().ShouldBeGreaterThan(0);
        estimate.GetProperty("estimatedBatches").GetInt32().ShouldBeGreaterThan(0);
        estimate.GetProperty("estimatedDurationSeconds").GetInt32().ShouldBeGreaterThan(0);
        estimate.GetProperty("storageAvailable").GetBoolean().ShouldBeTrue();

        // Batch telemetry was published on the (logical) telemetry subject.
        lines.ShouldContain(l => l.GetProperty("subject").GetString()!.StartsWith("mock.telemetry.batch."));

        // The final result (msgType=result) carries executedAt (same as the ack) and completedAt, and
        // its totalSamples is the ACTUAL count read back (15 seeded), distinct from the ack estimate.
        var result = lines.Single(l => MsgType(l) == "result");
        var resultData = result.GetProperty("payload").GetProperty("data");
        resultData.GetProperty("executedAt").GetInt64().ShouldBe(ackData.GetProperty("executedAt").GetInt64());
        resultData.GetProperty("completedAt").GetInt64().ShouldBeGreaterThanOrEqualTo(ackData.GetProperty("executedAt").GetInt64());
        resultData.GetProperty("resultData").GetProperty("totalSamples").GetInt32().ShouldBe(15);

        File.Delete(sinkPath);
    }

    /// <summary>Returns the payload's data.msgType, or null when the line has no command-response data.</summary>
    private static string? MsgType(JsonElement line)
    {
        if (line.GetProperty("payload").TryGetProperty("data", out var data)
            && data.TryGetProperty("msgType", out var msgType))
        {
            return msgType.GetString();
        }
        return null;
    }

    private static async Task SeedSensor(
        IRecordingService recordingService, string sensorId, DateTimeOffset start, int count, int intervalMs)
    {
        for (var i = 0; i < count; i++)
        {
            var ts = start.AddMilliseconds(i * intervalMs).ToUnixTimeMilliseconds();
            await recordingService.RecordAsync(sensorId, intervalMs, SchemaType.Double, ts, (double)i);
        }
    }

    private static string PathHelperResolve()
    {
        // Mirror BinaryRecordStorage's storage directory resolution. The context is built from an
        // empty configuration, so recording uses the default directory.
        return Weda.SubNode.Abstractions.Utilities.PathHelper.ResolveStorageDirectory(RecordingOptions.DefaultStorageDirectory);
    }

    private void WipeStorage()
    {
        try
        {
            if (Directory.Exists(_storageDir))
            {
                Directory.Delete(_storageDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a leftover dir only affects a re-run of this same test.
        }
    }
}

/// <summary>
/// Serialises every test that touches the shared, fixed RecordingService storage directory.
/// </summary>
[CollectionDefinition("RecordingStorage", DisableParallelization = true)]
public class RecordingStorageCollection { }

/// <summary>Bridges framework logging to xUnit test output for diagnosing the harness.</summary>
internal sealed class XunitLoggerProvider(ITestOutputHelper output) : Microsoft.Extensions.Logging.ILoggerProvider
{
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new XunitLogger(output, categoryName);
    public void Dispose() { }

    private sealed class XunitLogger(ITestOutputHelper output, string category) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try { output.WriteLine($"[{logLevel}] {category}: {formatter(state, exception)}{(exception is null ? "" : " | " + exception)}"); }
            catch { /* output unavailable outside an active test */ }
        }
    }
}
