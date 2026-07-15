namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Declares that a command handler is only meaningful when at least one
/// registered device implements one of the listed capability interfaces
/// (e.g. <c>IDigitalInputReadable</c>).
///
/// <para>Registration is unaffected — the handler is always registered and
/// dispatchable (its own runtime device lookup still guards execution).
/// What this attribute gates is <b>exposure</b>: when the SubNode
/// uploads its capability catalog to cloud, commands whose required
/// capability is not implemented by any configured device class are
/// excluded, so the cloud UI never offers a command the SubNode cannot
/// actually serve.</para>
///
/// <para>Handlers without this attribute (e.g. <c>report.data</c>,
/// <c>system.reboot</c>) are always exposed.</para>
///
/// <para>Convention: this attribute is a LIBRARY-code concern. Handlers
/// written in a SubNode application are authored for that SubNode's own
/// devices and should stay attribute-free (always exposed). Only handlers
/// shipped through libraries (the SDK's built-in I/O commands, reusable
/// device packages) reach SubNodes whose hardware may not support them, and
/// therefore need gating.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresDeviceCapabilityAttribute : Attribute
{
    /// <param name="capabilities">
    /// Capability interfaces required by the handler. The command is
    /// exposed when ANY configured device class implements ANY of them.
    /// </param>
    public RequiresDeviceCapabilityAttribute(params Type[] capabilities)
    {
        Capabilities = capabilities;
    }

    public IReadOnlyList<Type> Capabilities { get; }
}
