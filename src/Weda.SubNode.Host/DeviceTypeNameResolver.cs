using System.Reflection;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Default implementation of IDeviceTypeNameResolver.
/// Supports convention-based type resolution with priority order:
/// 1. Try as fully qualified name (e.g., "Namespace.ClassName, AssemblyName")
/// 2. Search in entry assembly (user's project) - HIGHEST PRIORITY
///    - Root namespace: "YourApp.MyFirstDevice"
///    - All types in assembly: "MyFirstDevice"
/// 3. Search in Weda.SubNode.Devices.Generic namespace (SDK built-in devices)
/// 4. Search in all other loaded assemblies (other dependencies)
/// </summary>
public class DefaultDeviceTypeNameResolver : IDeviceTypeNameResolver
{
    private readonly Assembly _entryAssembly;
    private readonly string? _entryNamespace;

    public DefaultDeviceTypeNameResolver()
    {
        _entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        _entryNamespace = _entryAssembly.GetName().Name;
    }

    public Type? Resolve(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        // 1. Try as fully qualified name first (e.g., "Namespace.Class, Assembly")
        var type = Type.GetType(typeName, throwOnError: false);
        if (type != null)
            return type;

        // 2. PRIORITY: Search in entry assembly (user's project) FIRST
        type = SearchInEntryAssembly(typeName);
        if (type != null)
            return type;

        // 3. Try in Weda.SubNode.Devices.Generic namespace (SDK built-in devices)
        type = Type.GetType($"Weda.SubNode.Devices.Generic.{typeName}, Weda.SubNode.Devices", throwOnError: false);
        if (type != null)
            return type;

        // 4. Search in all other loaded assemblies (other dependencies)
        return SearchInOtherAssemblies(typeName);
    }

    private Type? SearchInEntryAssembly(string typeName)
    {
        // Try with entry assembly's root namespace (e.g., "YourApp.MyFirstDevice")
        if (!string.IsNullOrEmpty(_entryNamespace))
        {
            var type = _entryAssembly.GetType($"{_entryNamespace}.{typeName}", throwOnError: false);
            if (type != null)
                return type;
        }

        // Search all types in entry assembly (handles nested namespaces)
        // This catches cases like "YourApp.Devices.MyFirstDevice"
        var allTypes = _entryAssembly.GetTypes();
        foreach (var t in allTypes)
        {
            if (t.Name == typeName)
                return t;
        }

        return null;
    }

    private Type? SearchInOtherAssemblies(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Skip entry assembly (already searched)
            if (assembly == _entryAssembly)
                continue;

            // Skip Weda.SubNode.Devices (already searched)
            if (assembly.GetName().Name == "Weda.SubNode.Devices")
                continue;

            // Try exact type name
            var type = assembly.GetType(typeName, throwOnError: false);
            if (type != null)
                return type;

            // Try with assembly's default namespace
            var assemblyName = assembly.GetName().Name;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                type = assembly.GetType($"{assemblyName}.{typeName}", throwOnError: false);
                if (type != null)
                    return type;
            }
        }

        return null;
    }
}
