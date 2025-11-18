namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Resolves device type names to actual Type instances.
/// Supports both short names (e.g., "TcpModbusDevice") and fully qualified names.
/// </summary>
public interface IDeviceTypeNameResolver
{
    /// <summary>
    /// Resolve a device type name to a Type instance.
    /// </summary>
    /// <param name="typeName">
    /// Device type name, can be:
    /// - Short name: "TcpModbusDevice", "MyFirstDevice"
    /// - Fully qualified: "Namespace.ClassName, AssemblyName"
    /// </param>
    /// <returns>The resolved Type, or null if not found</returns>
    Type? Resolve(string typeName);
}
