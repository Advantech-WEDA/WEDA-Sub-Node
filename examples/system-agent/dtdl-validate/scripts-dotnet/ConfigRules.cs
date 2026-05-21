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

            // ============================================================
            // gpio -- Parameters.PinId / Parameters.PinIds (INTEGER-ONLY)
            // ============================================================
            ["gpio"] = new Rule[]
            {
                PinIdShape,
                PinIdsShape,
                PinIdsMutualExclusivity,
                IsSupportedMustNotCarryPinFields,
            },

            // ============================================================
            // disk -- Parameters.MountPoint required (non-empty string)
            // ============================================================
            ["disk"] = new Rule[]
            {
                MountPointRequired,
            },

            // ============================================================
            // temperature -- Parameters.Source / Parameters.Sources
            // ============================================================
            ["temperature"] = new Rule[]
            {
                SourceShape,
                SourcesShape,
                SourceMutualExclusivity,
                V10CompatMustNotCarrySourceFields,
            },
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

    // ---------- gpio rules ----------

    private static RuleResult PinIdShape(JsonElement sensor)
    {
        if (!IsMetricName(sensor, "pinState")) return RuleResult.OkResult;
        if (!TryGetParameter(sensor, "PinId", out var pinId)) return RuleResult.OkResult;

        if (TryGetNonNegativeInt(pinId, out _)) return RuleResult.OkResult;
        return RuleResult.Fail(
            $"Parameters.PinId must be a non-negative integer; got {DescribeValue(pinId)}");
    }

    private static RuleResult PinIdsShape(JsonElement sensor)
    {
        if (!IsMetricName(sensor, "pinState")) return RuleResult.OkResult;
        if (!TryGetParameter(sensor, "PinIds", out var pinIds)) return RuleResult.OkResult;

        if (pinIds.ValueKind != JsonValueKind.Array)
            return RuleResult.Fail(
                $"Parameters.PinIds must be an array of non-negative integers; got {pinIds.ValueKind}");

        foreach (var item in pinIds.EnumerateArray())
        {
            if (!TryGetNonNegativeInt(item, out _))
                return RuleResult.Fail(
                    $"Parameters.PinIds element {DescribeValue(item)} is not a non-negative integer");
        }
        return RuleResult.OkResult;
    }

    private static RuleResult PinIdsMutualExclusivity(JsonElement sensor)
    {
        if (!IsMetricName(sensor, "pinState")) return RuleResult.OkResult;

        var hasPinId = TryGetParameter(sensor, "PinId", out _);
        var hasPinIds = false;
        if (TryGetParameter(sensor, "PinIds", out var pinIds) &&
            pinIds.ValueKind == JsonValueKind.Array)
        {
            hasPinIds = pinIds.GetArrayLength() > 0;
        }

        if (hasPinId && hasPinIds)
            return RuleResult.Fail(
                "Parameters.PinId and Parameters.PinIds both set non-empty; runtime would pick 'PinId' but the config is ambiguous");

        return RuleResult.OkResult;
    }

    private static RuleResult IsSupportedMustNotCarryPinFields(JsonElement sensor)
    {
        if (!IsMetricName(sensor, "isSupported")) return RuleResult.OkResult;
        if (TryGetParameter(sensor, "PinId", out _) ||
            TryGetParameter(sensor, "PinIds", out _))
        {
            return RuleResult.Fail(
                "metricName=isSupported sensors must not carry PinId or PinIds");
        }
        return RuleResult.OkResult;
    }

    private static bool IsMetricName(JsonElement sensor, string expected)
    {
        if (!TryGetParameter(sensor, "MetricName", out var mn)) return false;
        return mn.ValueKind == JsonValueKind.String &&
               string.Equals(mn.GetString(), expected, StringComparison.Ordinal);
    }

    private static bool TryGetNonNegativeInt(JsonElement v, out long value)
    {
        value = 0;
        if (v.ValueKind != JsonValueKind.Number) return false;
        if (!v.TryGetInt64(out value)) return false;
        if (value < 0) return false;
        // Reject decimals like 4.5 (TryGetInt64 returns false anyway but be explicit).
        var raw = v.GetRawText();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E')) return false;
        return true;
    }

    private static string DescribeValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => $"string \"{v.GetString()}\"",
        JsonValueKind.Null   => "null",
        _                    => $"{v.ValueKind} {v.GetRawText()}",
    };

    // ---------- disk rules ----------

    private static RuleResult MountPointRequired(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "MountPoint", out var mp))
            return RuleResult.Fail("disk sensors require Parameters.MountPoint (missing)");

        if (mp.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(mp.GetString()))
            return RuleResult.OkResult;

        return RuleResult.Fail(
            $"Parameters.MountPoint must be a non-empty string; got {DescribeValue(mp)}");
    }

    // ---------- temperature rules ----------

    private static RuleResult SourceShape(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "Source", out var src)) return RuleResult.OkResult;

        if (src.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(src.GetString()))
            return RuleResult.OkResult;

        return RuleResult.Fail(
            $"Parameters.Source must be a non-empty string; got {DescribeValue(src)}");
    }

    private static RuleResult SourcesShape(JsonElement sensor)
    {
        if (!TryGetParameter(sensor, "Sources", out var srcs)) return RuleResult.OkResult;

        if (srcs.ValueKind != JsonValueKind.Array)
            return RuleResult.Fail(
                $"Parameters.Sources must be an array of non-empty strings; got {srcs.ValueKind}");

        foreach (var item in srcs.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(item.GetString()))
                return RuleResult.Fail(
                    $"Parameters.Sources element {DescribeValue(item)} is not a non-empty string");
        }
        return RuleResult.OkResult;
    }

    private static RuleResult SourceMutualExclusivity(JsonElement sensor)
    {
        var hasSrc =
            TryGetParameter(sensor, "Source", out var src) &&
            src.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(src.GetString());

        var hasSrcs = false;
        if (TryGetParameter(sensor, "Sources", out var srcs) &&
            srcs.ValueKind == JsonValueKind.Array)
        {
            hasSrcs = srcs.GetArrayLength() > 0;
        }

        if (hasSrc && hasSrcs)
            return RuleResult.Fail(
                "Parameters.Source and Parameters.Sources both set non-empty; runtime would pick 'Source' but the config is ambiguous");

        return RuleResult.OkResult;
    }

    private static RuleResult V10CompatMustNotCarrySourceFields(JsonElement sensor)
    {
        if (IsMetricName(sensor, "therm")) return RuleResult.OkResult;

        var hasSrc = TryGetParameter(sensor, "Source", out _);
        var hasSrcs = TryGetParameter(sensor, "Sources", out _);
        if (!hasSrc && !hasSrcs) return RuleResult.OkResult;

        string mnDescription = "<missing>";
        if (TryGetParameter(sensor, "MetricName", out var mn) &&
            mn.ValueKind == JsonValueKind.String)
        {
            mnDescription = $"\"{mn.GetString()}\"";
        }
        return RuleResult.Fail(
            $"v1.0-compat temperature sensors (MetricName={mnDescription}) must not carry Source or Sources; the source name is the MetricName itself");
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
