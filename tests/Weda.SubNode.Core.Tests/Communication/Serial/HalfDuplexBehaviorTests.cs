using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;
using Xunit;

namespace Weda.SubNode.Core.Tests.Communication.Serial;

/// <summary>
/// Tests for half-duplex behavior verification.
/// Ensures concurrent requests are serialized through RequestLock.
/// </summary>
public class HalfDuplexBehaviorTests
{
    private readonly ILogger<CommunicationBase> _logger;

    public HalfDuplexBehaviorTests()
    {
        _logger = Substitute.For<ILogger<CommunicationBase>>();
    }

    [Fact]
    public async Task ConcurrentRequests_ShouldBeSerialized_NoOverlap()
    {
        // Arrange
        var executionLog = new List<(string phase, DateTime time)>();
        var communication = new TestCommunication(
            _logger,
            onRequest: async (request, ct) =>
            {
                var requestId = BitConverter.ToInt32(request, 0);
                lock (executionLog)
                {
                    executionLog.Add(($"Start-{requestId}", DateTime.UtcNow));
                }

                // Simulate device processing time
                await Task.Delay(50, ct);

                lock (executionLog)
                {
                    executionLog.Add(($"End-{requestId}", DateTime.UtcNow));
                }

                return request;
            });

        // Act - Launch 5 concurrent requests
        var tasks = Enumerable.Range(1, 5)
            .Select(i => communication.RequestAsync(BitConverter.GetBytes(i)))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Verify no overlap: each End should come before next Start
        executionLog.Count.ShouldBe(10); // 5 starts + 5 ends

        for (int i = 0; i < executionLog.Count - 1; i += 2)
        {
            var start = executionLog[i];
            var end = executionLog[i + 1];

            start.phase.ShouldStartWith("Start");
            end.phase.ShouldStartWith("End");

            // Extract request IDs
            var startId = start.phase.Split('-')[1];
            var endId = end.phase.Split('-')[1];
            startId.ShouldBe(endId, "Start and End should be for same request");
        }

        // Verify sequential execution: no overlapping time ranges
        var ranges = new List<(DateTime start, DateTime end)>();
        for (int i = 0; i < executionLog.Count; i += 2)
        {
            ranges.Add((executionLog[i].time, executionLog[i + 1].time));
        }

        for (int i = 0; i < ranges.Count - 1; i++)
        {
            // Current end should be <= next start (no overlap)
            ranges[i].end.ShouldBeLessThanOrEqualTo(
                ranges[i + 1].start,
                $"Request {i} overlapped with request {i + 1}");
        }
    }

    [Fact]
    public async Task SharedCommunication_MultipleDevices_RequestsSerialized()
    {
        // Arrange - Simulate multiple devices sharing one communication
        var activeRequests = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var communication = new TestCommunication(
            _logger,
            onRequest: async (request, ct) =>
            {
                lock (lockObj)
                {
                    activeRequests++;
                    maxConcurrent = Math.Max(maxConcurrent, activeRequests);
                }

                await Task.Delay(30, ct); // Simulate device I/O

                lock (lockObj)
                {
                    activeRequests--;
                }

                return request;
            });

        // Act - Simulate 3 devices making requests concurrently
        var device1Tasks = Enumerable.Range(1, 3)
            .Select(i => communication.RequestAsync([(byte)i]));
        var device2Tasks = Enumerable.Range(101, 3)
            .Select(i => communication.RequestAsync([(byte)i]));
        var device3Tasks = Enumerable.Range(201, 3)
            .Select(i => communication.RequestAsync([(byte)i]));

        await Task.WhenAll(device1Tasks.Concat(device2Tasks).Concat(device3Tasks));

        // Assert - Max concurrent should be 1 (serialized)
        maxConcurrent.ShouldBe(1, "Only one request should be active at a time (half-duplex)");
    }

    [Fact]
    public async Task RequestLock_Disabled_AllowsConcurrent()
    {
        // Arrange
        var activeRequests = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var settings = new ConnectionSettings { RequestLock = false };
        var communication = new TestCommunication(
            _logger,
            settings,
            onRequest: async (request, ct) =>
            {
                lock (lockObj)
                {
                    activeRequests++;
                    maxConcurrent = Math.Max(maxConcurrent, activeRequests);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    activeRequests--;
                }

                return request;
            });

        // Act - Launch concurrent requests
        var tasks = Enumerable.Range(1, 5)
            .Select(i => communication.RequestAsync([(byte)i]))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Should allow concurrent (max > 1)
        maxConcurrent.ShouldBeGreaterThan(1, "Without RequestLock, concurrent requests should be allowed");
    }

    [Fact]
    public async Task RequestLock_Timeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            RequestLock = true,
            ReadTimeoutMs = 100 // Short timeout for test
        };

        var communication = new TestCommunication(
            _logger,
            settings,
            onRequest: async (request, ct) =>
            {
                // Long operation that blocks the lock
                await Task.Delay(500, ct);
                return request;
            });

        // Act - First request holds the lock, second should timeout
        var firstRequest = communication.RequestAsync([0x01]);

        // Wait a bit for first request to acquire lock
        await Task.Delay(20);

        // Assert - Second request should timeout
        await Should.ThrowAsync<TimeoutException>(async () =>
            await communication.RequestAsync([0x02]));

        // Cleanup
        try { await firstRequest; } catch { }
    }

    /// <summary>
    /// Test communication implementation for verifying request serialization
    /// </summary>
    private class TestCommunication : RequestResponseCommunicationBase<byte[], byte[]>
    {
        private readonly Func<byte[], CancellationToken, Task<byte[]>> _onRequest;

        public TestCommunication(
            ILogger<CommunicationBase> logger,
            Func<byte[], CancellationToken, Task<byte[]>>? onRequest = null)
            : base(new ConnectionSettings { RequestLock = true }, logger)
        {
            _onRequest = onRequest ?? ((req, ct) => Task.FromResult(req));
        }

        public TestCommunication(
            ILogger<CommunicationBase> logger,
            ConnectionSettings settings,
            Func<byte[], CancellationToken, Task<byte[]>>? onRequest = null)
            : base(settings, logger)
        {
            _onRequest = onRequest ?? ((req, ct) => Task.FromResult(req));
        }

        protected override Task<byte[]> RequestAsyncCore(byte[] request, CancellationToken cancellationToken = default)
        {
            return _onRequest(request, cancellationToken);
        }

        protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task DisconnectAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
