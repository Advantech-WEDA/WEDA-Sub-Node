namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Capability descriptor for strongly-typed device configurations.
/// </summary>
/// <remarks>
/// Implemented alongside <see cref="IDeviceConfiguration"/> by classes such as
/// <c>TcpModbusDeviceConfiguration</c> to declare:
/// <list type="bullet">
///   <item>A stable type identifier (<see cref="DeviceTypeName"/>) used by cloud /
///         front-end to address this device kind.</item>
///   <item>Two POCO shapes — <typeparamref name="TCommunication"/> for transport-layer
///         fields (Host / Port / BrokerUrl ...) and <typeparamref name="TProperties"/>
///         for protocol-specific fields (SlaveId / ByteOrder ...).</item>
/// </list>
///
/// <para>The descriptor is consumed by the capability scanner at startup; emitted as a
/// DTDL v3 Interface (<c>Category = "Device"</c>) with two top-level Property bindings
/// — <c>Communication</c> and <c>Properties</c> — and uploaded inside
/// <c>DeviceCapDto</c>.</para>
///
/// <para>The interface deliberately has no <c>Create</c> method — device construction
/// stays in <see cref="IDeviceConfiguration.ToDeviceConfiguration"/> and
/// <see cref="IDeviceFactory"/> which retain access to DI for cloud / logger
/// dependencies.</para>
/// </remarks>
/// <typeparam name="TCommunication">
/// POCO describing transport-layer settings. Must be a reference type with a
/// parameterless constructor so the JSON round-trip in
/// <c>TypedParameterConverter</c> can rehydrate it from the on-disk dictionary.
/// </typeparam>
/// <typeparam name="TProperties">
/// POCO describing protocol-specific settings. Same constraints as
/// <typeparamref name="TCommunication"/>.
/// </typeparam>
public interface IConfigurableDevice<TCommunication, TProperties>
    where TCommunication : class, new()
    where TProperties : class, new()
{
    /// <summary>
    /// Stable identifier of this device kind, matched case-insensitively against
    /// cloud-side device type registrations (e.g. <c>"tcp-modbus"</c>,
    /// <c>"mqtt-isensing"</c>, <c>"opc-ua"</c>).
    /// </summary>
    static abstract string DeviceTypeName { get; }

    /// <summary>
    /// Human-readable description surfaced into the emitted DTDL Interface's
    /// <c>description</c> field. Returns <c>null</c> when no description is desired.
    /// </summary>
    static abstract string? Description { get; }
}
