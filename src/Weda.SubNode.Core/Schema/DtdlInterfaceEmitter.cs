using System.Text.Json.Nodes;

namespace Weda.SubNode.Core.Schema;

public static class DtdlInterfaceEmitter
{
    public sealed record Options(
        string Prefix,
        string Category,
        string TypeName,
        string? DisplayName = null,
        string? Description = null,
        int Version = 1);

    public sealed record PropertyBinding(string Name, Type Type, bool Writable = true);

    public static JsonObject Emit(Options opts, params PropertyBinding[] properties)
    {
        throw new NotImplementedException();
    }
}

public sealed class DtdlInterfaceEmissionException : Exception
{
    public DtdlInterfaceEmissionException(string message) : base(message) { }
    public DtdlInterfaceEmissionException(string message, Exception inner) : base(message, inner) { }
}
