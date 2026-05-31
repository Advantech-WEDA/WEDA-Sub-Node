namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Marks a runtime device class (i.e. an <see cref="IDevice"/> implementation
/// passed to <c>AddDevice&lt;TDevice&gt;("sectionName")</c>) with the
/// <c>DeviceTypeName</c> identifier it shares with a corresponding
/// <see cref="IConfigurableDevice{TComm, TProps}"/> declaration.
///
/// <para>The host loader uses this attribute to populate
/// <see cref="DeviceConfiguration.DeviceTypeName"/>, which in turn triggers the
/// typed sensor-dtmi dispatch path in
/// <c>DeviceConfiguration.InitializeDtdl</c>.</para>
///
/// <para>Phase-1 transitional design: <c>IConfigurableDevice</c> lives on the
/// strongly-typed configuration class (<c>TcpModbusDeviceConfiguration</c>) so
/// the DTDL Interface scan can emit Communication + Properties from its POCO
/// generics. This attribute is the link from a device class (which knows nothing
/// about those POCOs) to that registered name. Phase 2 / future cleanup may
/// unify the two onto one class and retire the attribute.</para>
///
/// <para>The attribute is <c>Inherited</c> so a base class such as
/// <c>TcpModbusDevice</c> can carry it and every concrete subclass
/// (e.g. <c>MyFirstDevice : TcpModbusDevice</c>) automatically inherits the
/// type name without restating it.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DeviceTypeAttribute(string deviceTypeName) : Attribute
{
    /// <summary>The DTDL device type identifier, e.g. <c>"tcp-modbus"</c>.</summary>
    public string DeviceTypeName { get; } = deviceTypeName;
}
