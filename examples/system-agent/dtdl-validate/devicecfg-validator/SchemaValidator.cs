using System.Text.Json;

namespace Weda.DeviceCfg.Validator;

/// <summary>Outcome of one validation run.</summary>
public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public Dictionary<string, bool> WritableMap { get; } = new();
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Validates a devicecfg.json payload against a ConfigConstraint-annotated DTDL
/// schema. The schema is read as raw JSON; the extension members are plain JSON
/// members on each Property (contents[]) and Field (Object fields[]).
///
/// Checks:
///   REQUIRED       -- required=true member MUST be present
///   ALLOWED VALUE  -- primitive type + Enum allow-list
///   STRUCTURE      -- Object / Array shape
///   VALUE RANGE    -- minimum / maximum
///   STRING LENGTH  -- minLength / maxLength
///   CSV TOLERANCE  -- csvStringAccepted: an array field also accepts a CSV /
///                     empty string
///   MUTEX          -- mutexGroup: sibling fields sharing a group are mutually
///                     exclusive (at most one present and non-empty)
///   CONDITIONAL    -- allowedOnlyWhen: a field may appear only when a named
///                     sibling field equals a given value
/// WRITABLE is collected for reporting, not a failure.
///
/// Schema kinds handled: string, integer, boolean, enum, object, array.
/// </summary>
public sealed class SchemaValidator
{
    private sealed record Bounds(double? Min, double? Max, int? MinLen, int? MaxLen)
    {
        public static readonly Bounds None = new(null, null, null, null);
    }

    private sealed record MemberRule(
        string Name, JsonElement SchemaRef, bool Required, bool Writable, Bounds Bounds,
        bool CsvStringAccepted, string? MutexGroup, string? WhenField, string? WhenEquals);

    private static readonly HashSet<string> Primitives = new(StringComparer.Ordinal)
    { "string", "integer", "boolean" };

    private readonly JsonElement _schemaRoot;
    private readonly Dictionary<string, JsonElement> _schemaMap = new(StringComparer.Ordinal);
    private readonly ValidationResult _r = new();

    public SchemaValidator(JsonElement schemaRoot)
    {
        _schemaRoot = schemaRoot;
        if (schemaRoot.TryGetProperty("schemas", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in arr.EnumerateArray())
                if (s.TryGetProperty("@id", out var id) && id.ValueKind == JsonValueKind.String)
                    _schemaMap[id.GetString()!] = s;
        }
    }

    /// <summary>Validate <paramref name="payload"/> against the schema's contents[].</summary>
    public ValidationResult Validate(JsonElement payload)
    {
        if (_schemaRoot.TryGetProperty("contents", out var contents)
            && contents.ValueKind == JsonValueKind.Array)
            CheckObject(payload, contents, "", "");
        else
            _r.Errors.Add("(schema): no contents[] array");
        return _r;
    }

    // ---- object / contents check ------------------------------------------
    private void CheckObject(JsonElement json, JsonElement membersArr, string jsonPath, string schemaPath)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var rules = new List<MemberRule>();

        foreach (var member in membersArr.EnumerateArray())
        {
            var name = GetStr(member, "name");
            if (name is null) continue;
            declared.Add(name);

            string? whenField = null, whenEquals = null;
            if (member.TryGetProperty("allowedOnlyWhen", out var aow) && aow.ValueKind == JsonValueKind.Object)
            {
                whenField  = GetStr(aow, "field");
                whenEquals = GetStr(aow, "equals");
            }

            rules.Add(new MemberRule(
                name,
                member.TryGetProperty("schema", out var sref) ? sref : default,
                GetBool(member, "required", false),
                GetBool(member, "writable", false),
                new Bounds(GetNum(member, "minimum"), GetNum(member, "maximum"),
                           (int?)GetNum(member, "minLength"), (int?)GetNum(member, "maxLength")),
                GetBool(member, "csvStringAccepted", false),
                GetStr(member, "mutexGroup"),
                whenField, whenEquals));
        }

        // Per-member: presence, required, value.
        foreach (var rule in rules)
        {
            var childSchemaPath = schemaPath.Length == 0 ? rule.Name : $"{schemaPath}.{rule.Name}";
            _r.WritableMap.TryAdd(childSchemaPath, rule.Writable);

            var childJsonPath = jsonPath.Length == 0 ? rule.Name : $"{jsonPath}.{rule.Name}";
            var present = json.TryGetProperty(rule.Name, out var v)
                          && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

            if (!present)
            {
                if (rule.Required) _r.Errors.Add($"{childJsonPath}: REQUIRED member is missing");
                continue;
            }
            if (rule.SchemaRef.ValueKind == JsonValueKind.Undefined)
            {
                _r.Errors.Add($"{childJsonPath}: schema definition missing in DTDL");
                continue;
            }
            CheckValue(v, rule.SchemaRef, rule.Bounds, rule.CsvStringAccepted, childJsonPath, childSchemaPath);
        }

        // Unknown keys.
        foreach (var p in json.EnumerateObject())
            if (!declared.Contains(p.Name))
                _r.Warnings.Add($"{(jsonPath.Length == 0 ? "" : jsonPath + ".")}{p.Name}: key not declared in the schema");

        // Cross-field: mutual exclusivity.
        foreach (var group in rules.Where(r => r.MutexGroup != null).GroupBy(r => r.MutexGroup!))
        {
            var set = group.Where(r => IsPresentNonEmpty(json, r.Name)).Select(r => r.Name).ToList();
            if (set.Count > 1)
                _r.Errors.Add($"{(jsonPath.Length == 0 ? "(root)" : jsonPath)}: fields [{string.Join(", ", set)}]"
                            + $" are mutually exclusive (mutexGroup '{group.Key}') -- at most one may be set");
        }

        // Cross-field: conditional presence.
        foreach (var rule in rules.Where(r => r.WhenField != null))
        {
            if (!IsPresent(json, rule.Name)) continue;
            var sib = json.TryGetProperty(rule.WhenField!, out var sv) ? ValueAsString(sv) : null;
            if (sib != rule.WhenEquals)
            {
                var childJsonPath = jsonPath.Length == 0 ? rule.Name : $"{jsonPath}.{rule.Name}";
                _r.Errors.Add($"{childJsonPath}: allowed only when {rule.WhenField}='{rule.WhenEquals}'"
                            + $" (found {rule.WhenField}='{sib ?? "<absent>"}')");
            }
        }
    }

    // ---- recursive value check --------------------------------------------
    private void CheckValue(JsonElement value, JsonElement schemaRef, Bounds bounds,
                            bool csvStringAccepted, string jsonPath, string schemaPath)
    {
        var (kind, def) = ResolveSchema(schemaRef);
        switch (kind)
        {
            case "string":
                if (value.ValueKind != JsonValueKind.String)
                { _r.Errors.Add($"{jsonPath}: expected string, got {value.ValueKind}"); break; }
                var len = (value.GetString() ?? "").Length;
                if (bounds.MinLen is int mn && len < mn)
                    _r.Errors.Add($"{jsonPath}: string length {len} < minLength {mn}");
                if (bounds.MaxLen is int mx && len > mx)
                    _r.Errors.Add($"{jsonPath}: string length {len} > maxLength {mx}");
                break;

            case "integer":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
                { _r.Errors.Add($"{jsonPath}: expected integer, got {value.ValueKind}"); break; }
                CheckRange(value.GetDouble(), bounds, jsonPath);
                break;

            case "boolean":
                if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    _r.Errors.Add($"{jsonPath}: expected boolean, got {value.ValueKind}");
                break;

            case "enum":
            {
                var allowed = new List<string>();
                if (def.TryGetProperty("enumValues", out var evs) && evs.ValueKind == JsonValueKind.Array)
                    foreach (var ev in evs.EnumerateArray())
                        if (ev.TryGetProperty("enumValue", out var x))
                            allowed.Add(x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText());
                var asStr = value.ValueKind == JsonValueKind.String ? value.GetString() ?? ""
                                                                    : value.GetRawText();
                if (!allowed.Contains(asStr))
                    _r.Errors.Add($"{jsonPath}: '{asStr}' not in allowed values [{string.Join(", ", allowed)}]");
                break;
            }

            case "object":
                if (value.ValueKind != JsonValueKind.Object)
                { _r.Errors.Add($"{jsonPath}: expected object, got {value.ValueKind}"); break; }
                if (def.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
                    CheckObject(value, fields, jsonPath, schemaPath);
                break;

            case "array":
                // csvStringAccepted: a comma-separated string (or empty string) is
                // accepted as the wire-tolerant form of the array.
                if (csvStringAccepted && value.ValueKind == JsonValueKind.String)
                    break;
                if (value.ValueKind != JsonValueKind.Array)
                { _r.Errors.Add($"{jsonPath}: expected array, got {value.ValueKind}"); break; }
                if (def.TryGetProperty("elementSchema", out var elemSchema))
                {
                    var i = 0;
                    foreach (var el in value.EnumerateArray())
                        CheckValue(el, elemSchema, Bounds.None, false, $"{jsonPath}[{i++}]", $"{schemaPath}[]");
                }
                break;

            default:
                _r.Errors.Add($"{jsonPath}: schema could not be resolved");
                break;
        }
    }

    private void CheckRange(double v, Bounds b, string jsonPath)
    {
        if (b.Min is double mn && v < mn) _r.Errors.Add($"{jsonPath}: value {Fmt(v)} < minimum {Fmt(mn)}");
        if (b.Max is double mx && v > mx) _r.Errors.Add($"{jsonPath}: value {Fmt(v)} > maximum {Fmt(mx)}");
    }

    // ---- schema resolution -------------------------------------------------
    private (string kind, JsonElement def) ResolveSchema(JsonElement schemaRef)
    {
        if (schemaRef.ValueKind == JsonValueKind.String)
        {
            var s = schemaRef.GetString()!;
            if (Primitives.Contains(s)) return (s, default);
            if (_schemaMap.TryGetValue(s, out var d)) return (StructuralKind(d), d);
            return ("?", default);
        }
        if (schemaRef.ValueKind == JsonValueKind.Object)   // inline schema
            return (StructuralKind(schemaRef), schemaRef);
        return ("?", default);
    }

    private static string StructuralKind(JsonElement def)
    {
        if (!def.TryGetProperty("@type", out var t)) return "?";
        if (t.ValueKind == JsonValueKind.String)
            return t.GetString()!.ToLowerInvariant();
        if (t.ValueKind == JsonValueKind.Array)
            foreach (var x in t.EnumerateArray())
            {
                var s = x.GetString();
                if (s is "Enum" or "Object" or "Array") return s.ToLowerInvariant();
            }
        return "?";
    }

    // ---- helpers -----------------------------------------------------------
    private static bool IsPresent(JsonElement json, string name)
        => json.TryGetProperty(name, out var v)
           && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static bool IsPresentNonEmpty(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => (v.GetString() ?? "").Length > 0,
            JsonValueKind.Array  => v.GetArrayLength() > 0,
            _                    => true,
        };
    }

    private static string? ValueAsString(JsonElement v)
        => v.ValueKind switch
        {
            JsonValueKind.String                          => v.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _                                             => v.GetRawText(),
        };

    private static string Fmt(double d) => d == Math.Floor(d) ? ((long)d).ToString() : d.ToString();

    private static bool GetBool(JsonElement o, string k, bool dflt)
    {
        if (o.TryGetProperty(k, out var v))
        {
            if (v.ValueKind == JsonValueKind.True)  return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return dflt;
    }

    private static double? GetNum(JsonElement o, string k)
        => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? GetStr(JsonElement o, string k)
        => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
