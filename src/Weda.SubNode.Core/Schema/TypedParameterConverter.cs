namespace Weda.SubNode.Core.Schema;

public static class TypedParameterConverter
{
    public static T FromDictionary<T>(IReadOnlyDictionary<string, object>? source)
        where T : class, new()
    {
        throw new NotImplementedException();
    }

    public static Dictionary<string, object> ToDictionary<T>(T value) where T : class
    {
        throw new NotImplementedException();
    }
}
