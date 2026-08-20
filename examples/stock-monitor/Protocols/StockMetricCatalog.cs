namespace StockMonitor.Protocols;

/// <summary>
/// The metrics a TWSE quote can be projected into, with the DTDL schema and unit each one reports.
/// </summary>
/// <remarks>
/// One sensor reports one metric, so the declared schema has to match that metric's runtime type:
/// a share count is a <c>long</c>, a price or a ratio is a <c>double</c>. Keeping the pairing here
/// rather than in <c>devicecfg.json</c> alone means the configuration can be checked against it
/// instead of the two drifting apart.
/// </remarks>
public static class StockMetricCatalog
{
    /// <summary>DTDL schema and unit reported for one metric.</summary>
    /// <param name="Schema">DTDL primitive schema, matching the value's runtime type.</param>
    /// <param name="Unit">Unit of measurement, surfaced in the capability upload.</param>
    public sealed record MetricSpec(string Schema, string Unit);

    private const string Twd = "TWD";

    private static readonly Dictionary<string, MetricSpec> Specs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Current"] = new("double", Twd),
            ["Open"] = new("double", Twd),
            ["High"] = new("double", Twd),
            ["Low"] = new("double", Twd),
            ["PreviousClose"] = new("double", Twd),
            ["Change"] = new("double", Twd),
            ["ChangePercent"] = new("double", "%"),
            ["Volume"] = new("long", "shares"),
        };

    /// <summary>The metric names this device understands.</summary>
    public static IReadOnlyCollection<string> Names => Specs.Keys;

    /// <summary>Whether <paramref name="metric"/> is a metric this device can report.</summary>
    public static bool IsKnown(string? metric) =>
        !string.IsNullOrWhiteSpace(metric) && Specs.ContainsKey(metric);

    /// <summary>Looks up a metric's schema and unit.</summary>
    /// <param name="metric">Metric name, case-insensitive.</param>
    /// <exception cref="ArgumentException">Thrown when the metric is not in the catalog.</exception>
    public static MetricSpec SpecFor(string metric) =>
        Specs.TryGetValue(metric ?? string.Empty, out var spec)
            ? spec
            : throw new ArgumentException(
                $"Unknown stock metric '{metric}'. Known metrics: {string.Join(", ", Specs.Keys)}.",
                nameof(metric));

    /// <summary>The DTDL schema a metric reports.</summary>
    public static string SchemaFor(string metric) => SpecFor(metric).Schema;

    /// <summary>The unit a metric reports.</summary>
    public static string UnitFor(string metric) => SpecFor(metric).Unit;
}
