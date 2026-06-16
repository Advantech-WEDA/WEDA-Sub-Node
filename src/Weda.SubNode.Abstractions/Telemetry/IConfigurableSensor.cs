namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Capability descriptor for strongly-typed sensor parameter classes.
/// </summary>
/// <remarks>
/// Implemented alongside a sensor-side parameters POCO to declare:
/// <list type="bullet">
///   <item>The parent device kind (<see cref="DeviceTypeName"/>) under which this
///         sensor shape is meaningful — sensor parameter shapes are typically
///         protocol-bound (a Modbus register sensor is only meaningful on a
///         Modbus device).</item>
///   <item>A stable identifier for this sensor shape (<see cref="SensorTypeName"/>)
///         scoped within the parent device.</item>
///   <item>One POCO shape — <typeparamref name="TParameter"/> — describing the
///         protocol-specific <c>Sensor.Parameters</c> fields (Modbus register
///         address / data type, MQTT topic, etc.).</item>
/// </list>
///
/// <para>The descriptor is consumed by the capability scanner at startup; emitted
/// as a DTDL v3 Interface (<c>Category = "Sensor"</c>,
/// <c>TypeName = "{deviceTypeName}:{sensorTypeName}"</c>) with one top-level
/// <c>Property</c> binding (<c>Parameters</c>) and uploaded inside
/// <c>DeviceCapDto</c>.</para>
///
/// <para>The interface deliberately has no <c>Create</c> method — sensors do not
/// have an independent instantiation lifecycle; the parent device owns sensor
/// reading. The descriptor declares schema only.</para>
/// </remarks>
/// <typeparam name="TParameter">
/// POCO describing per-sensor protocol-specific parameters. Must be a reference
/// type with a parameterless constructor so the JSON round-trip in
/// <c>TypedParameterConverter</c> can rehydrate it from the on-disk
/// <c>Sensor.Parameters</c> dictionary.
/// </typeparam>
public interface IConfigurableSensor<TParameter> 
    where TParameter : class, new()
{
    /// <summary>
    /// The parent device kind under which this sensor type is registered.
    /// MUST match the <c>DeviceTypeName</c> of an
    /// <see cref="Devices.IConfigurableDevice{TCommunication, TProperties}"/>
    /// implementation (e.g. <c>"tcp-modbus"</c>, <c>"mqtt-isensing"</c>).
    /// Matched case-insensitively against the device-type registration.
    /// </summary>
    static abstract string DeviceTypeName { get; }

    /// <summary>
    /// Stable identifier of this sensor shape within its parent device kind
    /// (e.g. <c>"modbus-register"</c>, <c>"opc-ua-node"</c>). Unique within
    /// the scope of <see cref="DeviceTypeName"/>.
    /// </summary>
    static abstract string SensorTypeName { get; }

    /// <summary>
    /// Human-readable description surfaced into the emitted DTDL Interface's
    /// <c>description</c> field. Returns <c>null</c> when no description is desired.
    /// </summary>
    static abstract string? Description { get; }
}