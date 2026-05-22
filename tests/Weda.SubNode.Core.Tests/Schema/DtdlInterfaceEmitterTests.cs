using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Core.Schema;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

public class DtdlInterfaceEmitterTests
{
    private static DtdlInterfaceEmitter.Options DefaultOpts(
        string typeName = "Sample", string category = "Test") =>
        new(Prefix: "dtmi:test:Demo", Category: category, TypeName: typeName);

    private static JsonObject? FindSchema(JsonObject iface, string dtmi) =>
        iface["schemas"]!.AsArray()
            .Cast<JsonObject>()
            .FirstOrDefault(s => (string?)s["@id"] == dtmi);

    // ─── 1. Interface envelope ──────────────────────────────────────────────

    private class Empty { }

    [Fact]
    public void Interface_uses_v3_context_and_correct_id()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(typeName: "MyType"),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        Assert.Equal("dtmi:dtdl:context;3", (string?)iface["@context"]);
        Assert.Equal("dtmi:test:Demo:Test:MyType;1", (string?)iface["@id"]);
        Assert.Equal("Interface", (string?)iface["@type"]);
    }

    [Fact]
    public void Custom_version_propagates_to_all_dtmis()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts() with { Version = 3 },
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        Assert.Equal("dtmi:test:Demo:Test:Sample;3", (string?)iface["@id"]);
        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;3");
        Assert.NotNull(paramsObj);
    }

    [Fact]
    public void DisplayName_defaults_to_humanized_type_name()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(typeName: "TcpModbus"),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        Assert.Equal("Tcp Modbus", (string?)iface["displayName"]);
    }

    [Fact]
    public void Explicit_display_name_and_description_override_defaults()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts() with { DisplayName = "Custom", Description = "abc" },
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        Assert.Equal("Custom", (string?)iface["displayName"]);
        Assert.Equal("abc", (string?)iface["description"]);
    }

    [Fact]
    public void Description_omitted_when_not_provided()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        Assert.False(iface.ContainsKey("description"));
    }

    [Fact]
    public void Empty_property_set_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DtdlInterfaceEmitter.Emit(DefaultOpts()));
    }

    // ─── 2. Contents[] property bindings ────────────────────────────────────

    [Fact]
    public void Property_binding_emits_contents_entry_with_dtmi_reference()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        var contents = iface["contents"]!.AsArray();
        Assert.Single(contents);
        var first = contents[0]!.AsObject();
        Assert.Equal("Property", (string?)first["@type"]);
        Assert.Equal("Parameters", (string?)first["name"]);
        Assert.Equal("dtmi:test:Demo:Test:Sample:Parameters;1", (string?)first["schema"]);
        Assert.True((bool?)first["writable"]);
    }

    [Fact]
    public void Two_property_bindings_emit_two_contents_entries()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(category: "Device", typeName: "TcpModbus"),
            new DtdlInterfaceEmitter.PropertyBinding("Communication", typeof(Empty)),
            new DtdlInterfaceEmitter.PropertyBinding("Properties", typeof(Empty), Writable: false));

        var contents = iface["contents"]!.AsArray();
        Assert.Equal(2, contents.Count);
        Assert.Equal("Communication", (string?)contents[0]!["name"]);
        Assert.Equal("Properties",    (string?)contents[1]!["name"]);
        Assert.False((bool?)contents[1]!["writable"]);
    }

    [Fact]
    public void Duplicate_property_name_throws()
    {
        Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty))));
    }

    [Fact]
    public void Non_class_top_level_property_throws()
    {
        Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(int))));
    }

    // ─── 3. Primitive field types ───────────────────────────────────────────

    private class PrimitiveBag
    {
        public string Text { get; init; } = "";
        public int Count { get; init; }
        public long Big { get; init; }
        public double Ratio { get; init; }
        public float Small { get; init; }
        public bool Flag { get; init; }
        public byte Byte { get; init; }
        public ushort Short { get; init; }
    }

    [Fact]
    public void Primitive_clr_types_map_to_inline_dtdl_primitives()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(PrimitiveBag)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var fields = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!, f => (string?)f["schema"]);

        Assert.Equal("string",  fields["text"]);
        Assert.Equal("integer", fields["count"]);
        Assert.Equal("long",    fields["big"]);
        Assert.Equal("double",  fields["ratio"]);
        Assert.Equal("float",   fields["small"]);
        Assert.Equal("boolean", fields["flag"]);
        Assert.Equal("integer", fields["byte"]);
        Assert.Equal("integer", fields["short"]);
    }

    // ─── 4. Enums ───────────────────────────────────────────────────────────

    private enum Color { Red, Green, Blue }

    private enum WireFormat
    {
        [EnumMember(Value = "big-endian")]    BigEndian,
        [EnumMember(Value = "little-endian")] LittleEndian,
    }

    private class WithEnum
    {
        public Color Hue { get; init; }
        public WireFormat Wire { get; init; }
    }

    [Fact]
    public void Enum_field_emits_standalone_enum_schema_referenced_by_dtmi()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithEnum)));

        var enumSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Color;1");
        Assert.NotNull(enumSchema);
        Assert.Equal("Enum",   (string?)enumSchema!["@type"]);
        Assert.Equal("string", (string?)enumSchema["valueSchema"]);

        var values = enumSchema["enumValues"]!.AsArray()
            .Cast<JsonObject>()
            .Select(o => (Name: (string)o["name"]!, EnumValue: (string)o["enumValue"]!))
            .ToArray();
        Assert.Equal(new[] { "Red", "Green", "Blue" }, values.Select(v => v.Name).ToArray());
        Assert.Equal(new[] { "Red", "Green", "Blue" }, values.Select(v => v.EnumValue).ToArray());
    }

    [Fact]
    public void EnumMember_value_overrides_default_enum_value()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithEnum)));

        var enumSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:WireFormat;1")!;
        var values = enumSchema["enumValues"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(o => (string)o["name"]!, o => (string)o["enumValue"]!);

        Assert.Equal("big-endian",    values["BigEndian"]);
        Assert.Equal("little-endian", values["LittleEndian"]);
    }

    private class WithDoubleEnum
    {
        public Color First { get; init; }
        public Color Second { get; init; }
    }

    [Fact]
    public void Repeated_enum_type_is_deduplicated_in_schemas()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithDoubleEnum)));

        var colorSchemas = iface["schemas"]!.AsArray()
            .Cast<JsonObject>()
            .Where(s => (string?)s["@type"] == "Enum" &&
                        ((string?)s["@id"])!.Contains(":Color;"))
            .ToArray();
        Assert.Single(colorSchemas);
    }

    // ─── 5. Nested objects ──────────────────────────────────────────────────

    private class Nested
    {
        public string Label { get; init; } = "";
        public int Count { get; init; }
    }

    private class WithNested
    {
        public Nested Child { get; init; } = new();
    }

    [Fact]
    public void Nested_class_emits_standalone_object_referenced_by_dtmi()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithNested)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var childField = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .Single(f => (string?)f["name"] == "child");
        Assert.Equal("dtmi:test:Demo:Test:Sample:Nested;1", (string?)childField["schema"]);

        var nestedObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Nested;1");
        Assert.NotNull(nestedObj);
        Assert.Equal("Object", (string?)nestedObj!["@type"]);
        var nestedFields = nestedObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!, f => (string?)f["schema"]);
        Assert.Equal("string",  nestedFields["label"]);
        Assert.Equal("integer", nestedFields["count"]);
    }

    private class WithTwoNestedSame
    {
        public Nested A { get; init; } = new();
        public Nested B { get; init; } = new();
    }

    [Fact]
    public void Shared_nested_type_appears_once_in_schemas()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithTwoNestedSame)));

        var nestedSchemas = iface["schemas"]!.AsArray()
            .Cast<JsonObject>()
            .Where(s => (string?)s["@id"] == "dtmi:test:Demo:Test:Sample:Nested;1")
            .ToArray();
        Assert.Single(nestedSchemas);
    }

    // ─── 6. Arrays ──────────────────────────────────────────────────────────

    private class WithArrays
    {
        public List<string> Tags { get; init; } = new();
        public int[] Indices { get; init; } = Array.Empty<int>();
        public List<Color> Hues { get; init; } = new();
    }

    [Fact]
    public void Array_of_primitive_emits_standalone_array_schema()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithArrays)));

        var arrSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:TagsList;1");
        Assert.NotNull(arrSchema);
        Assert.Equal("Array",  (string?)arrSchema!["@type"]);
        Assert.Equal("string", (string?)arrSchema["elementSchema"]);
    }

    [Fact]
    public void Csharp_array_treated_same_as_list()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithArrays)));

        var arrSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:IndicesList;1");
        Assert.NotNull(arrSchema);
        Assert.Equal("integer", (string?)arrSchema!["elementSchema"]);
    }

    [Fact]
    public void Array_of_enum_references_enum_dtmi_as_element_schema()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithArrays)));

        var arrSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:HuesList;1");
        Assert.NotNull(arrSchema);
        Assert.Equal("dtmi:test:Demo:Test:Sample:Color;1", (string?)arrSchema!["elementSchema"]);
    }

    // ─── 7. Dictionary → Map ────────────────────────────────────────────────

    private class WithMap
    {
        public Dictionary<string, int> Counters { get; init; } = new();
    }

    [Fact]
    public void Dictionary_of_string_to_T_emits_map_schema()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithMap)));

        var mapSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:CountersMap;1");
        Assert.NotNull(mapSchema);
        Assert.Equal("Map", (string?)mapSchema!["@type"]);
        Assert.Equal("string", (string?)mapSchema["mapKey"]!["schema"]);
        Assert.Equal("integer", (string?)mapSchema["mapValue"]!["schema"]);
    }

    // ─── 8. Field naming ────────────────────────────────────────────────────

    private class CasingBag
    {
        public string MyField { get; init; } = "";
        [JsonPropertyName("custom_name")] public string Override { get; init; } = "";
    }

    [Fact]
    public void Field_names_camel_case_by_default_with_jsonpropertyname_override()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(CasingBag)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var names = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .Select(f => (string)f["name"]!)
            .ToArray();

        Assert.Contains("myField", names);
        Assert.Contains("custom_name", names);
        Assert.DoesNotContain("override", names);
        Assert.DoesNotContain("Override", names);
    }

    // ─── 9. Description hint composition ────────────────────────────────────

    private class WithAnnotations
    {
        [Description("Sliding window size")]
        [Range(1, 100), DefaultValue(5)]
        public int Window { get; init; } = 5;

        [Required, RegularExpression(@"^\d+$")]
        public string Code { get; init; } = "";

        [StringLength(20, MinimumLength = 3)]
        public string Name { get; init; } = "";
    }

    [Fact]
    public void Range_default_required_pattern_echoed_into_description()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithAnnotations)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var byName = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!);

        var windowDesc = (string?)byName["window"]["description"] ?? "";
        Assert.Contains("Sliding window size", windowDesc);
        Assert.Contains("range 1..100",        windowDesc);
        Assert.Contains("default 5",           windowDesc);

        var codeDesc = (string?)byName["code"]["description"] ?? "";
        Assert.Contains("required",     codeDesc);
        Assert.Contains(@"pattern ^\d+$", codeDesc);

        var nameDesc = (string?)byName["name"]["description"] ?? "";
        Assert.Contains("length 3..20", nameDesc);
    }

    [Fact]
    public void Field_with_no_annotations_has_no_description_key()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(PrimitiveBag)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var firstField = paramsObj["fields"]!.AsArray()[0]!.AsObject();
        Assert.False(firstField.ContainsKey("description"));
    }

    // ─── 10. Failure modes ──────────────────────────────────────────────────

    private class WithDateTime { public DateTime When { get; init; } }
    private class WithGuid { public Guid Id { get; init; } }
    private class WithStream { public Stream Stream { get; init; } = null!; }
    private interface IFoo { }
    private class WithInterface { public IFoo Foo { get; init; } = null!; }

    private class Cycle
    {
        public string Name { get; init; } = "";
        public Cycle? Self { get; init; }
    }

    [Fact]
    public void DateTime_field_throws()
    {
        var ex = Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithDateTime))));
        Assert.Contains("DateTime", ex.Message);
    }

    [Fact]
    public void Guid_field_throws()
    {
        var ex = Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithGuid))));
        Assert.Contains("Guid", ex.Message);
    }

    [Fact]
    public void Abstract_field_type_throws()
    {
        var ex = Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithStream))));
        Assert.Contains("polymorphism", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interface_field_type_throws()
    {
        Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithInterface))));
    }

    [Fact]
    public void Recursive_poco_throws()
    {
        var ex = Assert.Throws<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Cycle))));
        Assert.Contains("Circular", ex.Message);
    }

    // ─── 11. Nullable & display attribute ───────────────────────────────────

    private class WithNullable
    {
        public int? Maybe { get; init; }
        public Color? OptColor { get; init; }
    }

    [Fact]
    public void Nullable_value_type_unwraps_to_inner_schema()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithNullable)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var byName = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!, f => (string?)f["schema"]);

        Assert.Equal("integer", byName["maybe"]);
        Assert.Equal("dtmi:test:Demo:Test:Sample:Color;1", byName["optColor"]);
    }

    private class WithDisplay
    {
        [Display(Name = "Pretty Label")]
        public int Value { get; init; }
    }

    [Fact]
    public void Display_name_overrides_humanized_field_name()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithDisplay)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var field = paramsObj["fields"]!.AsArray()[0]!.AsObject();
        Assert.Equal("Pretty Label", (string?)field["displayName"]);
    }

    // ─── 12. Two-POCO emission (device shape) ───────────────────────────────

    private class Comm
    {
        [Required] public string Host { get; init; } = "";
        [Range(1, 65535), DefaultValue(502)] public int Port { get; init; } = 502;
    }

    private class Props
    {
        [Range(1, 247)] public byte SlaveId { get; init; } = 1;
    }

    [Fact]
    public void Device_shape_emits_two_top_level_objects_in_schemas()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(category: "Device", typeName: "TcpModbus"),
            new DtdlInterfaceEmitter.PropertyBinding("Communication", typeof(Comm)),
            new DtdlInterfaceEmitter.PropertyBinding("Properties", typeof(Props)));

        Assert.NotNull(FindSchema(iface, "dtmi:test:Demo:Device:TcpModbus:Communication;1"));
        Assert.NotNull(FindSchema(iface, "dtmi:test:Demo:Device:TcpModbus:Properties;1"));

        var contents = iface["contents"]!.AsArray();
        Assert.Equal(2, contents.Count);
        Assert.Equal("dtmi:test:Demo:Device:TcpModbus:Communication;1",
            (string?)contents[0]!["schema"]);
        Assert.Equal("dtmi:test:Demo:Device:TcpModbus:Properties;1",
            (string?)contents[1]!["schema"]);
    }
}
