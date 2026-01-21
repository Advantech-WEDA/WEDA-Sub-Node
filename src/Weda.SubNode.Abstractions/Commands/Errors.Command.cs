using ErrorOr;

namespace Weda.SubNode.Abstractions.Commands;

public static partial class Errors
{
    public static class Command
    {
        public static Error ValidationFailed(string message) => Error.Validation(
            code: "Command.ValidationFailed",
            description: message);

        public static Error HandlerNotFound(string message) => Error.NotFound(
            code: "Command.HandlerNotFound",
            description: message);

        public static Error ExecutionFailed(string message) => Error.Failure(
            code: "Command.ExecutionFailed",
            description: message);
    }
}