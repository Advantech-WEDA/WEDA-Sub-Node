namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Toggles DTDL-based runtime payload validation (Step 0) in
/// <see cref="CommandDispatcher"/>. Bound from configuration section
/// <c>DtdlValidation</c>.
/// </summary>
/// <remarks>
/// Default <see cref="Enabled"/> is <c>false</c>: production keeps the
/// per-command <see cref="Weda.Dtdl.Validation.WedaDtValidator"/> walk off
/// to avoid extra latency, since handlers already get DataAnnotation
/// validation downstream. Turn it on in dev / test / staging to catch
/// contract drift between SubNode and cloud-published DTDL.
/// </remarks>
public sealed class DtdlValidationOptions
{
    public const string SectionName = "DtdlValidation";

    public bool Enabled { get; set; }
}
