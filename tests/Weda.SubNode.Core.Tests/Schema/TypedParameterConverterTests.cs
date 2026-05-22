using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

using Weda.SubNode.Core.Schema;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

public class TypedParameterConverterTests
{
    // ─── 1. Round-trip primitives ───────────────────────────────────────────

    private class SimpleParameters
    {
        public string Name { get; init; } = "";
        public int Count { get; init; }
        public bool Flag { get; init; }
        public double Ratio { get; init; }
    }

    [Fact]
    public void FromDictionary_populates_primitive_fields()
    {
        var dict = new Dictionary<string, object>
        {
            ["name"] = "hello",
            ["count"] = 7,
            ["flag"] = true,
            ["ratio"] = 1.5,
        };

        var typed = TypedParameterConverter.FromDictionary<SimpleParameters>(dict);

        Assert.Equal("hello", typed.Name);
        Assert.Equal(7, typed.Count);
        Assert.True(typed.Flag);
        Assert.Equal(1.5, typed.Ratio);
    }

    [Fact]
    public void FromDictionary_accepts_case_insensitive_property_names()
    {
        var dict = new Dictionary<string, object>
        {
            ["Name"] = "hello",
            ["COUNT"] = 3,
        };

        var typed = TypedParameterConverter.FromDictionary<SimpleParameters>(dict);

        Assert.Equal("hello", typed.Name);
        Assert.Equal(3, typed.Count);
    }

    [Fact]
    public void ToDictionary_produces_camel_case_keys()
    {
        var value = new SimpleParameters { Name = "x", Count = 9, Flag = true, Ratio = 2.0 };

        var dict = TypedParameterConverter.ToDictionary(value);

        Assert.Contains("name", dict.Keys);
        Assert.Contains("count", dict.Keys);
        Assert.Contains("flag", dict.Keys);
        Assert.Contains("ratio", dict.Keys);
    }

    [Fact]
    public void Round_trip_preserves_values()
    {
        var original = new SimpleParameters { Name = "round", Count = 42, Flag = true, Ratio = 3.14 };
        var dict = TypedParameterConverter.ToDictionary(original);
        var restored = TypedParameterConverter.FromDictionary<SimpleParameters>(dict);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Count, restored.Count);
        Assert.Equal(original.Flag, restored.Flag);
        Assert.Equal(original.Ratio, restored.Ratio);
    }

    // ─── 2. Null / empty source ─────────────────────────────────────────────

    private class WithDefaults
    {
        public int Window { get; init; } = 5;
        public string Mode { get; init; } = "auto";
    }

    [Fact]
    public void FromDictionary_null_source_uses_poco_defaults()
    {
        var typed = TypedParameterConverter.FromDictionary<WithDefaults>(null);

        Assert.Equal(5, typed.Window);
        Assert.Equal("auto", typed.Mode);
    }

    [Fact]
    public void FromDictionary_empty_dict_uses_poco_defaults()
    {
        var typed = TypedParameterConverter.FromDictionary<WithDefaults>(
            new Dictionary<string, object>());

        Assert.Equal(5, typed.Window);
        Assert.Equal("auto", typed.Mode);
    }

    [Fact]
    public void FromDictionary_partial_dict_keeps_unset_defaults()
    {
        var typed = TypedParameterConverter.FromDictionary<WithDefaults>(
            new Dictionary<string, object> { ["window"] = 11 });

        Assert.Equal(11, typed.Window);
        Assert.Equal("auto", typed.Mode);
    }

    [Fact]
    public void ToDictionary_null_value_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TypedParameterConverter.ToDictionary<SimpleParameters>(null!));
    }

    // ─── 3. Enums ───────────────────────────────────────────────────────────

    private enum Mode { Auto, Manual, Test }

    private class WithEnum
    {
        public Mode Mode { get; init; }
    }

    [Fact]
    public void FromDictionary_parses_enum_from_string()
    {
        var typed = TypedParameterConverter.FromDictionary<WithEnum>(
            new Dictionary<string, object> { ["mode"] = "Manual" });

        Assert.Equal(Mode.Manual, typed.Mode);
    }

    [Fact]
    public void ToDictionary_serialises_enum_as_string()
    {
        var dict = TypedParameterConverter.ToDictionary(new WithEnum { Mode = Mode.Test });

        var element = Assert.IsType<JsonElement>(dict["mode"]);
        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Equal("Test", element.GetString());
    }

    // ─── 4. Nested objects & collections ────────────────────────────────────

    private class Inner
    {
        public string Label { get; init; } = "";
        public int Count { get; init; }
    }

    private class Outer
    {
        public Inner Child { get; init; } = new();
        public List<int> Tags { get; init; } = new();
    }

    [Fact]
    public void FromDictionary_populates_nested_object_via_json_round_trip()
    {
        var dict = new Dictionary<string, object>
        {
            ["child"] = new Dictionary<string, object>
            {
                ["label"] = "L",
                ["count"] = 3,
            },
            ["tags"] = new[] { 1, 2, 3 },
        };

        var typed = TypedParameterConverter.FromDictionary<Outer>(dict);

        Assert.Equal("L", typed.Child.Label);
        Assert.Equal(3, typed.Child.Count);
        Assert.Equal(new[] { 1, 2, 3 }, typed.Tags);
    }

    // ─── 5. JsonPropertyName override ───────────────────────────────────────

    private class WithCustomName
    {
        [JsonPropertyName("slave_id")]
        public byte SlaveId { get; init; }
    }

    [Fact]
    public void FromDictionary_honours_JsonPropertyName_for_input_keys()
    {
        var typed = TypedParameterConverter.FromDictionary<WithCustomName>(
            new Dictionary<string, object> { ["slave_id"] = 7 });

        Assert.Equal((byte)7, typed.SlaveId);
    }

    [Fact]
    public void ToDictionary_honours_JsonPropertyName_for_output_keys()
    {
        var dict = TypedParameterConverter.ToDictionary(new WithCustomName { SlaveId = 9 });

        Assert.Contains("slave_id", dict.Keys);
        Assert.DoesNotContain("slaveId", dict.Keys);
    }

    // ─── 6. DataAnnotation validation ───────────────────────────────────────

    private class WithRange
    {
        [Range(1, 100)] public int Window { get; init; }
    }

    [Fact]
    public void FromDictionary_throws_validation_exception_when_range_violated()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithRange>(
                new Dictionary<string, object> { ["window"] = 9999 }));

        Assert.Contains("Window", ex.Message);
    }

    [Fact]
    public void FromDictionary_passes_when_value_within_range()
    {
        var typed = TypedParameterConverter.FromDictionary<WithRange>(
            new Dictionary<string, object> { ["window"] = 50 });

        Assert.Equal(50, typed.Window);
    }

    private class WithRequired
    {
        [Required] public string Host { get; init; } = "";
    }

    [Fact]
    public void FromDictionary_throws_when_required_field_is_empty()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithRequired>(
                new Dictionary<string, object>()));

        Assert.Contains("Host", ex.Message);
    }

    private class WithPattern
    {
        [RegularExpression(@"^\d+$")] public string Code { get; init; } = "";
    }

    [Fact]
    public void FromDictionary_validates_regex()
    {
        Assert.Throws<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithPattern>(
                new Dictionary<string, object> { ["code"] = "abc" }));
    }

    // ─── 7. IValidatableObject (cross-field rules) ──────────────────────────

    private class WithCrossField : IValidatableObject
    {
        public string? Interface { get; init; }
        public List<string>? Interfaces { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext _)
        {
            var hasSingle = !string.IsNullOrEmpty(Interface);
            var hasList = Interfaces is { Count: > 0 };
            if (hasSingle && hasList)
            {
                yield return new ValidationResult(
                    "Interface and Interfaces are mutually exclusive",
                    new[] { nameof(Interface), nameof(Interfaces) });
            }
        }
    }

    [Fact]
    public void FromDictionary_runs_IValidatableObject_cross_field_rules()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithCrossField>(
                new Dictionary<string, object>
                {
                    ["interface"] = "eth0",
                    ["interfaces"] = new[] { "eth1", "eth2" },
                }));

        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void FromDictionary_passes_when_cross_field_rule_satisfied()
    {
        var typed = TypedParameterConverter.FromDictionary<WithCrossField>(
            new Dictionary<string, object> { ["interface"] = "eth0" });

        Assert.Equal("eth0", typed.Interface);
        Assert.Null(typed.Interfaces);
    }

    // ─── 8. Validation error aggregation ────────────────────────────────────

    private class WithMultipleRules
    {
        [Required] public string Name { get; init; } = "";
        [Range(1, 10)] public int Count { get; init; }
    }

    [Fact]
    public void FromDictionary_reports_every_validation_failure()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithMultipleRules>(
                new Dictionary<string, object> { ["count"] = 999 }));

        Assert.Contains("Name", ex.Message);
        Assert.Contains("Count", ex.Message);
    }
}
