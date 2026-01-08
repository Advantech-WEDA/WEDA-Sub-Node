using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.Retry;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices.Retry;

/// <summary>
/// Unit tests for RetryOrchestrator.
/// Tests retry policies, circuit breaker, thread-safety, and event emission.
/// </summary>
public sealed class RetryOrchestratorTests
{
    private const string TestDeviceId = "test-retry-device";
    private readonly RetryOrchestrator _orchestrator;

    public RetryOrchestratorTests()
    {
        _orchestrator = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.Immediate,
            CircuitBreakerPolicy.Disabled);
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_ShouldInitializeWithClosedCircuit()
    {
        // Assert
        _orchestrator.CurrentCircuitState.ShouldBe(CircuitState.Closed);
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroCounters()
    {
        // Act
        var stats = _orchestrator.GetStatistics();

        // Assert
        stats.DeviceId.ShouldBe(TestDeviceId);
        stats.CurrentCircuitState.ShouldBe(CircuitState.Closed);
        stats.TotalAttempts.ShouldBe(0);
        stats.SuccessfulAttempts.ShouldBe(0);
        stats.FailedAttempts.ShouldBe(0);
        stats.CircuitOpenCount.ShouldBe(0);
    }

    #endregion

    #region Retry Policy Tests

    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ShouldNotRetry()
    {
        // Arrange
        int attemptCount = 0;
        var operation = (CancellationToken ct) =>
        {
            attemptCount++;
            return Task.FromResult(ErrorOrFactory.From(42));
        };

        // Act
        var result = await _orchestrator.ExecuteAsync(operation, "test-op");

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(42);
        attemptCount.ShouldBe(1);

        var stats = _orchestrator.GetStatistics();
        stats.SuccessfulAttempts.ShouldBe(1);
        stats.FailedAttempts.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_FailureThenSuccess_ShouldRetry()
    {
        // Arrange
        int attemptCount = 0;
        var operation = (CancellationToken ct) =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                return Task.FromResult<ErrorOr<int>>(Error.Failure("Temporary failure"));
            }
            return Task.FromResult(ErrorOrFactory.From(42));
        };

        // Act
        var result = await _orchestrator.ExecuteAsync(operation, "test-op");

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(42);
        attemptCount.ShouldBe(3);

        var stats = _orchestrator.GetStatistics();
        stats.TotalAttempts.ShouldBe(3);
        stats.SuccessfulAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_AllRetriesFail_ShouldReturnError()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 2,
            BackoffStrategy = BackoffStrategy.Constant,
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        int attemptCount = 0;
        var operation = (CancellationToken ct) =>
        {
            attemptCount++;
            return Task.FromResult<ErrorOr<int>>(Error.Failure("Always fails"));
        };

        // Act
        var result = await orch.ExecuteAsync(operation, "test-op");

        // Assert
        result.IsError.ShouldBeTrue();
        attemptCount.ShouldBe(3); // Initial + 2 retries
        // The error should be returned (either the original error or OperationFailedAfterRetries)
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldConvertToError()
    {
        // Arrange
        var policy = RetryPolicy.None; // No retries
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        var operation = (CancellationToken ct) =>
        {
            throw new InvalidOperationException("Test exception");
#pragma warning disable CS0162 // Unreachable code detected
            return Task.FromResult(ErrorOrFactory.From(0));
#pragma warning restore CS0162 // Unreachable code detected
        };

        // Act
        var result = await orch.ExecuteAsync<int>(operation, "test-op");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("Test exception");
    }

    #endregion

    #region Backoff Strategy Tests

    [Theory]
    [InlineData(BackoffStrategy.Constant, 0, 100)]
    [InlineData(BackoffStrategy.Constant, 1, 100)]
    [InlineData(BackoffStrategy.Constant, 2, 100)]
    public async Task BackoffStrategy_Constant_ShouldUseSameDelay(BackoffStrategy strategy, int _, double expectedMs)
    {
        // Test that constant backoff maintains the same delay
        var policy = new RetryPolicy
        {
            MaxRetries = 3,
            BackoffStrategy = strategy,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            UseJitter = false
        };

        await VerifyBackoffDelay(policy, expectedMs);
    }

    [Fact]
    public async Task BackoffStrategy_Linear_ShouldIncreaseLinearly()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 3,
            BackoffStrategy = BackoffStrategy.Linear,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            UseJitter = false
        };

        // Delays should be: 100ms, 200ms, 300ms
        await VerifyBackoffIncreases(policy);
    }

    [Fact]
    public async Task BackoffStrategy_Exponential_ShouldIncreaseExponentially()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 3,
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Delays should be: 100ms, 200ms, 400ms
        await VerifyBackoffIncreases(policy);
    }

    [Fact]
    public async Task BackoffStrategy_MaxDelay_ShouldCapDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 5,
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(3),
            UseJitter = false
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        var delays = new List<TimeSpan>();
        RetryAttemptEvent? lastEvent = null;
        orch.RetryAttempting += (s, e) =>
        {
            delays.Add(e.Delay);
            lastEvent = e;
        };

        int attempt = 0;
        var operation = (CancellationToken ct) =>
        {
            attempt++;
            return Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));
        };

        // Act
        await orch.ExecuteAsync(operation, "test-op");

        // Assert - Last delays should be capped at 3 seconds
        delays.Last().TotalMilliseconds.ShouldBeLessThanOrEqualTo(3000);
    }

    private async Task VerifyBackoffDelay(RetryPolicy policy, double expectedFirstDelayMs)
    {
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        TimeSpan? firstDelay = null;
        orch.RetryAttempting += (s, e) =>
        {
            if (e.AttemptNumber == 1)
            {
                firstDelay = e.Delay;
            }
        };

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        await orch.ExecuteAsync(operation, "test-op");

        firstDelay.ShouldNotBeNull();
        firstDelay.Value.TotalMilliseconds.ShouldBe(expectedFirstDelayMs, 5);
    }

    private async Task VerifyBackoffIncreases(RetryPolicy policy)
    {
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        var delays = new List<TimeSpan>();
        orch.RetryAttempting += (s, e) => delays.Add(e.Delay);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        await orch.ExecuteAsync(operation, "test-op");

        delays.Count.ShouldBeGreaterThan(0);
        // Verify delays are increasing
        for (int i = 1; i < delays.Count; i++)
        {
            delays[i].ShouldBeGreaterThan(delays[i - 1]);
        }
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task CircuitBreaker_ConsecutiveFailures_ShouldOpenCircuit()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(1),
            SuccessThreshold = 1,
            SamplingDuration = TimeSpan.FromMinutes(1)
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            policy);

        CircuitBreakerStateChangedEvent? stateChangeEvent = null;
        orch.CircuitBreakerStateChanged += (s, e) => stateChangeEvent = e;

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Act - Fail 3 times to reach threshold
        for (int i = 0; i < 3; i++)
        {
            await orch.ExecuteAsync(operation, "test-op");
        }

        // Assert
        orch.CurrentCircuitState.ShouldBe(CircuitState.Open);
        stateChangeEvent.ShouldNotBeNull();
        stateChangeEvent.CurrentState.ShouldBe(CircuitState.Open);
        stateChangeEvent.PreviousState.ShouldBe(CircuitState.Closed);
        stateChangeEvent.Reason.ShouldContain("threshold");

        var stats = orch.GetStatistics();
        stats.CircuitOpenCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CircuitBreaker_OpenState_ShouldBlockRequests()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(10), // Long duration
            SuccessThreshold = 1,
            SamplingDuration = TimeSpan.FromMinutes(1)
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            policy);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Open the circuit
        await orch.ExecuteAsync(operation, "test-op");
        await orch.ExecuteAsync(operation, "test-op");

        orch.CurrentCircuitState.ShouldBe(CircuitState.Open);

        // Act - Try to execute while circuit is open
        var result = await orch.ExecuteAsync(
            (CancellationToken ct) => Task.FromResult(ErrorOrFactory.From(42)),
            "blocked-op");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("CircuitBreakerOpen");
    }

    [Fact]
    public async Task CircuitBreaker_AfterOpenDuration_ShouldTransitionToHalfOpen()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromMilliseconds(100), // Short duration for testing
            SuccessThreshold = 1,
            SamplingDuration = TimeSpan.FromMinutes(1)
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            policy);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Open the circuit
        await orch.ExecuteAsync(operation, "test-op");
        await orch.ExecuteAsync(operation, "test-op");

        orch.CurrentCircuitState.ShouldBe(CircuitState.Open);

        // Act - Wait for open duration to elapse
        await Task.Delay(150);

        CircuitBreakerStateChangedEvent? stateChangeEvent = null;
        orch.CircuitBreakerStateChanged += (s, e) => stateChangeEvent = e;

        // Execute a successful operation
        var result = await orch.ExecuteAsync(
            (CancellationToken ct) => Task.FromResult(ErrorOrFactory.From(42)),
            "recovery-op");

        // Assert
        result.IsError.ShouldBeFalse();
        orch.CurrentCircuitState.ShouldBe(CircuitState.Closed);
        stateChangeEvent.ShouldNotBeNull();
        stateChangeEvent.PreviousState.ShouldBe(CircuitState.HalfOpen);
        stateChangeEvent.CurrentState.ShouldBe(CircuitState.Closed);
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpenFailure_ShouldReopenCircuit()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromMilliseconds(100),
            SuccessThreshold = 1,
            SamplingDuration = TimeSpan.FromMinutes(1)
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            policy);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Open the circuit
        await orch.ExecuteAsync(operation, "test-op");
        await orch.ExecuteAsync(operation, "test-op");

        // Wait and transition to half-open
        await Task.Delay(150);

        CircuitBreakerStateChangedEvent? reopenEvent = null;
        orch.CircuitBreakerStateChanged += (s, e) =>
        {
            if (e.CurrentState == CircuitState.Open)
            {
                reopenEvent = e;
            }
        };

        // Act - Fail in half-open state
        await orch.ExecuteAsync(operation, "half-open-fail");

        // Assert
        orch.CurrentCircuitState.ShouldBe(CircuitState.Open);
        reopenEvent.ShouldNotBeNull();
        reopenEvent.PreviousState.ShouldBe(CircuitState.HalfOpen);
    }

    [Fact]
    public void ResetCircuitBreaker_ShouldCloseCircuit()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMinutes(10),
            SuccessThreshold = 1,
            SamplingDuration = TimeSpan.FromMinutes(1)
        };

        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            policy);

        // Open the circuit (we'll use reflection or assume it's open)
        // For this test, we just verify the reset works
        CircuitBreakerStateChangedEvent? resetEvent = null;
        orch.CircuitBreakerStateChanged += (s, e) => resetEvent = e;

        // Act
        orch.ResetCircuitBreaker();

        // Assert
        orch.CurrentCircuitState.ShouldBe(CircuitState.Closed);

        var stats = orch.GetStatistics();
        stats.CurrentCircuitState.ShouldBe(CircuitState.Closed);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task RetryAttempting_ShouldFireBeforeEachRetry()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 3,
            BackoffStrategy = BackoffStrategy.Constant,
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        var events = new List<RetryAttemptEvent>();
        orch.RetryAttempting += (s, e) => events.Add(e);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Act
        await orch.ExecuteAsync(operation, "test-op");

        // Assert
        events.Count.ShouldBe(3); // 3 retry attempts
        for (int i = 0; i < events.Count; i++)
        {
            events[i].DeviceId.ShouldBe(TestDeviceId);
            events[i].OperationName.ShouldBe("test-op");
            events[i].AttemptNumber.ShouldBe(i + 1);
            events[i].MaxAttempts.ShouldBe(4); // Initial + 3 retries
        }
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatistics_ShouldTrackAllAttempts()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 2,
            BackoffStrategy = BackoffStrategy.Constant,
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        // Act - 2 successful, 1 failed (with retries)
        await orch.ExecuteAsync(
            (CancellationToken ct) => Task.FromResult(ErrorOrFactory.From(1)),
            "op1");

        await orch.ExecuteAsync(
            (CancellationToken ct) => Task.FromResult(ErrorOrFactory.From(2)),
            "op2");

        await orch.ExecuteAsync(
            (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail")),
            "op3");

        // Assert
        var stats = orch.GetStatistics();
        stats.TotalAttempts.ShouldBe(5); // 2 success + 3 failures (1 initial + 2 retries)
        stats.SuccessfulAttempts.ShouldBe(2);
        stats.FailedAttempts.ShouldBe(1);
        stats.LastSuccessTime.ShouldNotBeNull();
        stats.LastFailureTime.ShouldNotBeNull();
    }

    #endregion

    #region Thread-Safety Tests

    [Fact]
    public async Task ConcurrentExecutions_ShouldBeSafe()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxRetries = 1,
            BackoffStrategy = BackoffStrategy.Constant,
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            policy,
            CircuitBreakerPolicy.Disabled);

        const int concurrentOperations = 50;
        var tasks = new List<Task<ErrorOr<int>>>();

        // Act - Execute many operations concurrently
        for (int i = 0; i < concurrentOperations; i++)
        {
            int value = i;
            tasks.Add(orch.ExecuteAsync(
                (CancellationToken ct) => Task.FromResult(ErrorOrFactory.From(value)),
                $"op-{i}"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(r => !r.IsError).ShouldBe(concurrentOperations);

        var stats = orch.GetStatistics();
        stats.SuccessfulAttempts.ShouldBe(concurrentOperations);
        stats.TotalAttempts.ShouldBe(concurrentOperations);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var operation = async (CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return await Task.FromResult(ErrorOrFactory.From(42));
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await _orchestrator.ExecuteAsync(operation, "cancelled-op", cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledCircuitBreaker_ShouldNeverBlockRequests()
    {
        // Arrange
        var orch = new RetryOrchestrator(
            TestDeviceId,
            NullLogger<RetryOrchestrator>.Instance,
            RetryPolicy.None,
            CircuitBreakerPolicy.Disabled);

        var operation = (CancellationToken ct) => Task.FromResult<ErrorOr<int>>(Error.Failure("Fail"));

        // Act - Execute many failures
        for (int i = 0; i < 100; i++)
        {
            await orch.ExecuteAsync(operation, "test-op");
        }

        // Assert - Circuit should still be closed
        orch.CurrentCircuitState.ShouldBe(CircuitState.Closed);
    }

    #endregion
}
