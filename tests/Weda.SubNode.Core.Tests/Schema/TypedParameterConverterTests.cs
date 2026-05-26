using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

using Shouldly;

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

        typed.Name.ShouldBe("hello");
        typed.Count.ShouldBe(7);
        typed.Flag.ShouldBeTrue();
        typed.Ratio.ShouldBe(1.5);
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

        typed.Name.ShouldBe("hello");
        typed.Count.ShouldBe(3);
    }

    [Fact]
    public void ToDictionary_produces_camel_case_keys()
    {
        var value = new SimpleParameters { Name = "x", Count = 9, Flag = true, Ratio = 2.0 };

        var dict = TypedParameterConverter.ToDictionary(value);

        dict.Keys.ShouldContain("name");
        dict.Keys.ShouldContain("count");
        dict.Keys.ShouldContain("flag");
        dict.Keys.ShouldContain("ratio");
    }

    [Fact]
    public void Round_trip_preserves_values()
    {
        var original = new SimpleParameters { Name = "round", Count = 42, Flag = true, Ratio = 3.14 };
        var dict = TypedParameterConverter.ToDictionary(original);
        var restored = TypedParameterConverter.FromDictionary<SimpleParameters>(dict);

        restored.Name.ShouldBe(original.Name);
        restored.Count.ShouldBe(original.Count);
        restored.Flag.ShouldBe(original.Flag);
        restored.Ratio.ShouldBe(original.Ratio);
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

        typed.Window.ShouldBe(5);
        typed.Mode.ShouldBe("auto");
    }

    [Fact]
    public void FromDictionary_empty_dict_uses_poco_defaults()
    {
        var typed = TypedParameterConverter.FromDictionary<WithDefaults>(
            new Dictionary<string, object>());

        typed.Window.ShouldBe(5);
        typed.Mode.ShouldBe("auto");
    }

    [Fact]
    public void FromDictionary_partial_dict_keeps_unset_defaults()
    {
        var typed = TypedParameterConverter.FromDictionary<WithDefaults>(
            new Dictionary<string, object> { ["window"] = 11 });

        typed.Window.ShouldBe(11);
        typed.Mode.ShouldBe("auto");
    }

    [Fact]
    public void ToDictionary_null_value_throws()
    {
        Should.Throw<ArgumentNullException>(() =>
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

        typed.Mode.ShouldBe(Mode.Manual);
    }

    [Fact]
    public void ToDictionary_serialises_enum_as_string()
    {
        var dict = TypedParameterConverter.ToDictionary(new WithEnum { Mode = Mode.Test });

        var element = dict["mode"].ShouldBeOfType<JsonElement>();
        element.ValueKind.ShouldBe(JsonValueKind.String);
        element.GetString().ShouldBe("Test");
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

        typed.Child.Label.ShouldBe("L");
        typed.Child.Count.ShouldBe(3);
        typed.Tags.ShouldBe(new[] { 1, 2, 3 });
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

        typed.SlaveId.ShouldBe((byte)7);
    }

    [Fact]
    public void ToDictionary_honours_JsonPropertyName_for_output_keys()
    {
        var dict = TypedParameterConverter.ToDictionary(new WithCustomName { SlaveId = 9 });

        dict.Keys.ShouldContain("slave_id");
        dict.Keys.ShouldNotContain("slaveId");
    }

    // ─── 6. DataAnnotation validation ───────────────────────────────────────

    private class WithRange
    {
        [Range(1, 100)] public int Window { get; init; }
    }

    [Fact]
    public void FromDictionary_throws_validation_exception_when_range_violated()
    {
        var ex = Should.Throw<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithRange>(
                new Dictionary<string, object> { ["window"] = 9999 }));

        ex.Message.ShouldContain("Window");
    }

    [Fact]
    public void FromDictionary_passes_when_value_within_range()
    {
        var typed = TypedParameterConverter.FromDictionary<WithRange>(
            new Dictionary<string, object> { ["window"] = 50 });

        typed.Window.ShouldBe(50);
    }

    private class WithRequired
    {
        [Required] public string Host { get; init; } = "";
    }

    [Fact]
    public void FromDictionary_throws_when_required_field_is_empty()
    {
        var ex = Should.Throw<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithRequired>(
                new Dictionary<string, object>()));

        ex.Message.ShouldContain("Host");
    }

    private class WithPattern
    {
        [RegularExpression(@"^\d+$")] public string Code { get; init; } = "";
    }

    [Fact]
    public void FromDictionary_validates_regex()
    {
        Should.Throw<ValidationException>(() =>
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
        var ex = Should.Throw<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithCrossField>(
                new Dictionary<string, object>
                {
                    ["interface"] = "eth0",
                    ["interfaces"] = new[] { "eth1", "eth2" },
                }));

        ex.Message.ShouldContain("mutually exclusive");
    }

    [Fact]
    public void FromDictionary_passes_when_cross_field_rule_satisfied()
    {
        var typed = TypedParameterConverter.FromDictionary<WithCrossField>(
            new Dictionary<string, object> { ["interface"] = "eth0" });

        typed.Interface.ShouldBe("eth0");
        typed.Interfaces.ShouldBeNull();
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
        var ex = Should.Throw<ValidationException>(() =>
            TypedParameterConverter.FromDictionary<WithMultipleRules>(
                new Dictionary<string, object> { ["count"] = 999 }));

        ex.Message.ShouldContain("Name");
        ex.Message.ShouldContain("Count");
    }
}
