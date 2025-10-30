using ErrorOr;

namespace Weda.SubNode.Abstractions.Devices;

public static partial class Errors
{
    public static class Device
    {
        // ===== Connection Errors =====

        public static Error PhysicalDeviceFailed => Error.Failure(
            code: "Connection.PhysicalDevice",
            description: "Failed to connect to physical device after retries");

        public static Error CloudServiceFailed => Error.Failure(
            code: "Connection.CloudService",
            description: "Failed to connect to cloud service after retries");

        public static Error NotConnected => Error.Validation(
            code: "Connection.State",
            description: "Device is not connected");

        public static Error SubscriptionFailed => Error.Failure(
            code: "Connection.Subscription",
            description: "Failed to subscribe to cloud events");

        public static Error Unexpected(Exception exception) => Error.Unexpected(
            code: "Connection.Unexpected",
            description: $"Unexpected error during connection: {exception.Message}");

        // ===== State Machine Errors =====

        public static Error InvalidStateTransition(DeviceStatus from, DeviceStatus to) => Error.Validation(
            code: "Device.InvalidStateTransition",
            description: $"Cannot transition from {from} to {to}");

        // ===== Lifecycle Errors =====

        public static Error InvalidLifecyclePhase(string current, string expected) => Error.Validation(
            code: "Device.InvalidLifecyclePhase",
            description: $"Cannot perform operation in phase '{current}', expected '{expected}'");

        public static Error InitializationFailed(Exception exception) => Error.Failure(
            code: "Device.InitializationFailed",
            description: $"Device initialization failed: {exception.Message}");

        // ===== Telemetry Pipeline Errors =====

        public static Error TransformStageFailed(Exception exception) => Error.Failure(
            code: "Device.TransformStageFailed",
            description: $"Transform stage failed: {exception.Message}");

        public static Error FilterStageFailed(Exception exception) => Error.Failure(
            code: "Device.FilterStageFailed",
            description: $"Filter stage failed: {exception.Message}");

        public static Error PipelineExecutionFailed(Exception exception) => Error.Failure(
            code: "Device.PipelineExecutionFailed",
            description: $"Pipeline execution failed: {exception.Message}");

        // ===== Retry & Circuit Breaker Errors =====

        public static Error CircuitBreakerOpen(string operation, TimeSpan remaining) => Error.Failure(
            code: "Device.CircuitBreakerOpen",
            description: $"Circuit breaker open for '{operation}', retry in {remaining.TotalSeconds:F1}s");

        public static Error OperationFailedAfterRetries(string operation, int attempts, Exception exception) => Error.Failure(
            code: "Device.OperationFailedAfterRetries",
            description: $"'{operation}' failed after {attempts} attempts: {exception.Message}");
    }
}
