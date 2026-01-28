namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Interface for command handler results.
/// Allows handlers to provide status code and message for command responses.
/// </summary>
public interface IResult
{
    /// <summary>
    /// Status code for the command result.
    /// See <see cref="Contracts.CommandResponseStatusCode"/> for standard values.
    /// </summary>
    int Status { get; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    string Message { get; }
}