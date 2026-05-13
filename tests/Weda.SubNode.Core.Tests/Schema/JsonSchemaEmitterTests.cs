using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Core.Schema;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

public class JsonSchemaEmitterTests
{
    private class Empty { }

    [Fact]
    public void Empty_class_emits_object_with_no_properties()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(Empty)).Root;

        Assert.Equal("object", (string?)schema["type"]);
        Assert.NotNull(schema["properties"]);
        Assert.Empty(schema["properties"]!.AsObject());
        Assert.Null(schema["required"]);
    }

    private class PrimitiveBag
    {
        public string Text { get; init; } = "";
        public int Count { get; init; }
        public long Big { get; init; }
        public double Ratio { get; init; }
        public bool Flag { get; init; }
    }

    [Fact]
    public void Primitive_types_map_to_correct_json_types()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(PrimitiveBag)).Root;
        var props = schema["properties"]!.AsObject();

        Assert.Equal("string", (string?)props["text"]!["type"]);
        Assert.Equal("integer", (string?)props["count"]!["type"]);
        Assert.Equal("integer", (string?)props["big"]!["type"]);
        Assert.Equal("number", (string?)props["ratio"]!["type"]);
        Assert.Equal("boolean", (string?)props["flag"]!["type"]);
    }

    private class RequiredFields
    {
        [Required] public string Must { get; init; } = "";
        public string Optional { get; init; } = "";
    }

    [Fact]
    public void Required_attribute_adds_property_to_required_array()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(RequiredFields)).Root;
        var required = schema["required"]!.AsArray();

        Assert.Single(required);
        Assert.Equal("must", (string?)required[0]);
    }

    private class RangedNumber
    {
        [Range(0, 100)] public int Value { get; init; }
    }

    [Fact]
    public void Range_attribute_emits_minimum_and_maximum()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(RangedNumber)).Root;
        var prop = schema["properties"]!["value"]!;

        Assert.Equal(0, (int?)prop["minimum"]);
        Assert.Equal(100, (int?)prop["maximum"]);
    }

    private class StringConstraints
    {
        [StringLength(20, MinimumLength = 3)] public string Code { get; init; } = "";
        [RegularExpression(@"^\d+$")] public string Digits { get; init; } = "";
        [MinLength(2), MaxLength(5)] public string Name { get; init; } = "";
    }

    [Fact]
    public void String_attributes_emit_length_and_pattern()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(StringConstraints)).Root;
        var props = schema["properties"]!.AsObject();

        Assert.Equal(20, (int?)props["code"]!["maxLength"]);
        Assert.Equal(3, (int?)props["code"]!["minLength"]);
        Assert.Equal(@"^\d+$", (string?)props["digits"]!["pattern"]);
        Assert.Equal(2, (int?)props["name"]!["minLength"]);
        Assert.Equal(5, (int?)props["name"]!["maxLength"]);
    }

    private enum Color { Red, Green, Blue }

    private class WithEnum
    {
        public Color Hue { get; init; }
    }

    [Fact]
    public void Enum_property_emits_string_type_with_enum_array()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithEnum)).Root;
        var prop = schema["properties"]!["hue"]!;

        Assert.Equal("string", (string?)prop["type"]);
        var values = prop["enum"]!.AsArray().Select(n => (string?)n).ToArray();
        Assert.Equal(new[] { "Red", "Green", "Blue" }, values);
    }

    private class WithAllowedValues
    {
        [AllowedValues("a", "b", "c")] public string Pick { get; init; } = "";
    }

    [Fact]
    public void AllowedValues_emits_enum_array()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithAllowedValues)).Root;
        var enumArr = schema["properties"]!["pick"]!["enum"]!.AsArray();

        Assert.Equal(new[] { "a", "b", "c" }, enumArr.Select(n => (string?)n).ToArray());
    }

    private class Nested
    {
        public string Name { get; init; } = "";
    }

    private class Outer
    {
        public Nested Child { get; init; } = new();
    }

    [Fact]
    public void Nested_class_property_recursively_emits_object_schema()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(Outer)).Root;
        var child = schema["properties"]!["child"]!.AsObject();

        Assert.Equal("object", (string?)child["type"]);
        Assert.NotNull(child["properties"]!["name"]);
    }

    private class WithList
    {
        public List<Nested> Items { get; init; } = new();
        public int[] Indices { get; init; } = [];
    }

    [Fact]
    public void Arrays_and_lists_emit_array_with_items_schema()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithList)).Root;
        var items = schema["properties"]!["items"]!;
        var indices = schema["properties"]!["indices"]!;

        Assert.Equal("array", (string?)items["type"]);
        Assert.Equal("object", (string?)items["items"]!["type"]);
        Assert.Equal("array", (string?)indices["type"]);
        Assert.Equal("integer", (string?)indices["items"]!["type"]);
    }

    private class WithDict
    {
        public Dictionary<string, int> Counts { get; init; } = new();
    }

    [Fact]
    public void Dictionary_emits_additionalProperties()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithDict)).Root;
        var prop = schema["properties"]!["counts"]!;

        Assert.Equal("object", (string?)prop["type"]);
        Assert.Equal("integer", (string?)prop["additionalProperties"]!["type"]);
    }

    private class WithCasingOverride
    {
        [JsonPropertyName("custom_name")] public string MyField { get; init; } = "";
        public string AnotherProp { get; init; } = "";
    }

    [Fact]
    public void JsonPropertyName_overrides_camelCase_conversion()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithCasingOverride)).Root;
        var props = schema["properties"]!.AsObject();

        Assert.NotNull(props["custom_name"]);
        Assert.Null(props["myField"]);
        Assert.NotNull(props["anotherProp"]);
    }

    private class WithDescription
    {
        [Description("Human description")] public string Name { get; init; } = "";
    }

    [Fact]
    public void Description_attribute_emits_description_field()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithDescription)).Root;
        Assert.Equal("Human description",
            (string?)schema["properties"]!["name"]!["description"]);
    }

    private class WithDefault
    {
        [DefaultValue(42)] public int Answer { get; init; } = 42;
    }

    [Fact]
    public void DefaultValue_attribute_emits_default_field()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithDefault)).Root;
        Assert.Equal(42, (int?)schema["properties"]!["answer"]!["default"]);
    }

    private class WithObjectMember
    {
        public object Anything { get; init; } = null!;
    }

    [Fact]
    public void Object_property_emits_empty_open_schema()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithObjectMember)).Root;
        var prop = schema["properties"]!["anything"]!.AsObject();

        // Empty schema {} = "any value is valid"
        Assert.Null(prop["type"]);
        Assert.Empty(prop);
    }

    private class WithJsonElementMember
    {
        public System.Text.Json.JsonElement Raw { get; init; }
    }

    [Fact]
    public void JsonElement_property_emits_empty_open_schema()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithJsonElementMember)).Root;
        var prop = schema["properties"]!["raw"]!.AsObject();

        Assert.Null(prop["type"]);
        Assert.Empty(prop);
    }

    private class WithAbstractMember
    {
        public Stream Stream { get; init; } = null!;
    }

    [Fact]
    public void Abstract_type_property_throws_at_emit_time()
    {
        var ex = Assert.Throws<JsonSchemaEmissionException>(
            () => JsonSchemaEmitter.Emit(typeof(WithAbstractMember)));
        Assert.Contains("abstract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private interface IFoo { }
    private class WithInterfaceMember
    {
        public IFoo Foo { get; init; } = null!;
    }

    [Fact]
    public void Interface_type_property_throws_at_emit_time()
    {
        var ex = Assert.Throws<JsonSchemaEmissionException>(
            () => JsonSchemaEmitter.Emit(typeof(WithInterfaceMember)));
        Assert.Contains("polymorphism", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private class WithDateTime
    {
        public DateTime When { get; init; }
    }

    [Fact]
    public void DateTime_property_throws_at_emit_time_with_clear_message()
    {
        var ex = Assert.Throws<JsonSchemaEmissionException>(
            () => JsonSchemaEmitter.Emit(typeof(WithDateTime)));
        Assert.Contains("DateTime", ex.Message);
    }

    private class WithGuid
    {
        public Guid Id { get; init; }
    }

    [Fact]
    public void Guid_property_throws_at_emit_time()
    {
        var ex = Assert.Throws<JsonSchemaEmissionException>(
            () => JsonSchemaEmitter.Emit(typeof(WithGuid)));
        Assert.Contains("Guid", ex.Message);
    }

    private class WithNullable
    {
        public int? Maybe { get; init; }
        public string? Optional { get; init; }
    }

    [Fact]
    public void Nullable_value_type_unwraps_to_inner_type()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(WithNullable)).Root;
        Assert.Equal("integer", (string?)schema["properties"]!["maybe"]!["type"]);
        Assert.Equal("string", (string?)schema["properties"]!["optional"]!["type"]);
    }

    [Fact]
    public void Nullable_does_not_imply_required()
    {
        // Rule A: only [Required] attribute determines required.
        var schema = JsonSchemaEmitter.Emit(typeof(WithNullable)).Root;
        Assert.Null(schema["required"]);
    }

    private class Cycle
    {
        public string Name { get; init; } = "";
        public Cycle? Self { get; init; }
    }

    [Fact]
    public void Recursive_type_throws_with_clear_error()
    {
        var ex = Assert.Throws<JsonSchemaEmissionException>(
            () => JsonSchemaEmitter.Emit(typeof(Cycle)));
        Assert.Contains("Circular", ex.Message);
    }

    [Fact]
    public void Schema_serializes_as_inline_json_object_without_wrapper()
    {
        var schema = JsonSchemaEmitter.Emit(typeof(PrimitiveBag));
        var json = System.Text.Json.JsonSerializer.Serialize(schema);
        var parsed = JsonNode.Parse(json)!.AsObject();

        // Wrapper invisible — direct schema keys at the top level
        Assert.Equal("object", (string?)parsed["type"]);
        Assert.NotNull(parsed["properties"]);
        Assert.Null(parsed["Root"]);
        Assert.Null(parsed["root"]);
    }
}
