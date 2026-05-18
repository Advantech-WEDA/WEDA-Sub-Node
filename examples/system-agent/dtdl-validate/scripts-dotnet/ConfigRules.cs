using System.Text.Json;

namespace Weda.Dtdl.Validator;

/// <summary>
/// Sensor-config shape rules.  Each rule reads a Sensor entry (devicecfg.json
/// Sensors[] element) and returns either <see cref="RuleResult.Ok"/> or
/// <see cref="RuleResult.Fail(string)"/>.  Rules are grouped by
/// Parameters.MetricType; a sensor whose MetricType has no registered rule
/// set passes through unchecked.
///
/// Mirror of dtdl-validate/scripts/config-rules.js -- keep the two in sync.
/// </summary>
public static class ConfigRules
{
    public readonly record struct RuleResult(bool Ok, string? Reason)
    {
        public static RuleResult OkResult { get; } = new(true, null);
        public static RuleResult Fail(string reason) => new(false, reason);
    }

    public delegate RuleResult Rule(JsonElement sensor);

    /// <summary>
    /// Rule sets keyed by MetricType. Extend by adding more keys / rules.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Rule>> Rules =
        new Dictionary<string, IReadOnlyList<Rule>>
        {
            // ============================================================
            // network -- Parameters.Interface / Parameters.Interfaces
            // ============================================================
            ["network"] = new Rule[]
            {
                InterfacesShape,
                InterfaceShape,
                MutualExclusivity,
            },

            // Extension points: add disk / temperature / gpio rules here.
        };

    public static RuleResult Validate(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "MetricType", out var mt) ||
            mt.ValueKind != JsonValueKind.String)
            return RuleResult.OkResult; // no MetricType -> no rules apply

        if (!Rules.TryGetValue(mt.GetString()!, out var ruleSet))
            return RuleResult.OkResult; // unknown type -> pass through

        foreach (var rule in ruleSet)
        {
            var res = rule(sensor);
            if (!res.Ok) return res;
        }
        return RuleResult.OkResult;
    }

    // ---------- rule implementations ----------

    private static RuleResult InterfacesShape(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "Interfaces", out var ifaces))
            return RuleResult.OkResult;

        if (ifaces.ValueKind == JsonValueKind.String) return RuleResult.OkResult;

        if (ifaces.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ifaces.EnumerateArray())
                if (item.ValueKind != JsonValueKind.String)
                    return RuleResult.Fail(
                        "Parameters.Interfaces must be string or array<string>; got array with non-string element");
            return RuleResult.OkResult;
        }

        return RuleResult.Fail(
            $"Parameters.Interfaces must be string or array<string>; got {ifaces.ValueKind}");
    }

    private static RuleResult InterfaceShape(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "Interface", out var iface))
            return RuleResult.OkResult;

        if (iface.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(iface.GetString()))
            return RuleResult.OkResult;

        var detail = iface.ValueKind == JsonValueKind.String ? "\"\"" : iface.ValueKind.ToString();
        return RuleResult.Fail($"Parameters.Interface must be a non-empty string; got {detail}");
    }

    private static RuleResult MutualExclusivity(JsonElement sensor)
    {
        var hasIface =
            TryGetParameter(sensor, "Interface", out var iface) &&
            iface.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(iface.GetString());

        var hasIfaces = false;
        if (TryGetParameter(sensor, "Interfaces", out var ifaces))
        {
            hasIfaces = ifaces.ValueKind switch
            {
                JsonValueKind.String => !string.IsNullOrEmpty(ifaces.GetString()),
                JsonValueKind.Array  => ifaces.GetArrayLength() > 0,
                _                    => false,
            };
        }

        if (hasIface && hasIfaces)
            return RuleResult.Fail(
                "Parameters.Interface and Parameters.Interfaces both set; runtime would pick 'Interface' but the config is ambiguous");

        return RuleResult.OkResult;
    }

    // ---------- helpers ----------

    private static bool TryGetParameter(JsonElement sensor, string key, out JsonElement value)
    {
        value = default;
        if (sensor.ValueKind != JsonValueKind.Object) return false;
        if (!sensor.TryGetProperty("Parameters", out var parameters)) return false;
        if (parameters.ValueKind != JsonValueKind.Object) return false;
        return parameters.TryGetProperty(key, out value);
    }
}
