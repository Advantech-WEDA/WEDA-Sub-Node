using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Shouldly;

using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;
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

        ((string?)iface["@context"]).ShouldBe("dtmi:dtdl:context;3");
        ((string?)iface["@id"]).ShouldBe("dtmi:test:Demo:Test:MyType;1");
        ((string?)iface["@type"]).ShouldBe("Interface");
    }

    [Fact]
    public void Custom_version_propagates_to_all_dtmis()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts() with { Version = 3 },
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        ((string?)iface["@id"]).ShouldBe("dtmi:test:Demo:Test:Sample;3");
        FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;3").ShouldNotBeNull();
    }

    [Fact]
    public void DisplayName_omitted_when_not_provided()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(typeName: "TcpModbus"),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        iface.ContainsKey("displayName").ShouldBeFalse();
    }

    [Fact]
    public void Explicit_display_name_and_description_override_defaults()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts() with { DisplayName = "Custom", Description = "abc" },
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        ((string?)iface["displayName"]).ShouldBe("Custom");
        ((string?)iface["description"]).ShouldBe("abc");
    }

    [Fact]
    public void Description_omitted_when_not_provided()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        iface.ContainsKey("description").ShouldBeFalse();
    }

    [Fact]
    public void Property_entry_omits_displayName_when_not_authored()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        var first = iface["contents"]!.AsArray()[0]!.AsObject();
        first.ContainsKey("displayName").ShouldBeFalse();
    }

    [Fact]
    public void Object_schema_omits_displayName_when_not_authored()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        paramsObj.ContainsKey("displayName").ShouldBeFalse();
    }

    [Fact]
    public void Empty_property_set_throws()
    {
        Should.Throw<ArgumentException>(() =>
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
        contents.Count.ShouldBe(1);
        var first = contents[0]!.AsObject();
        ((string?)first["@type"]).ShouldBe("Property");
        ((string?)first["name"]).ShouldBe("Parameters");
        ((string?)first["schema"]).ShouldBe("dtmi:test:Demo:Test:Sample:Parameters;1");
        ((bool?)first["writable"]).ShouldBe(true);
    }

    [Fact]
    public void Two_property_bindings_emit_two_contents_entries()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(category: "Device", typeName: "TcpModbus"),
            new DtdlInterfaceEmitter.PropertyBinding("Communication", typeof(Empty)),
            new DtdlInterfaceEmitter.PropertyBinding("Properties", typeof(Empty), Writable: false));

        var contents = iface["contents"]!.AsArray();
        contents.Count.ShouldBe(2);
        ((string?)contents[0]!["name"]).ShouldBe("Communication");
        ((string?)contents[1]!["name"]).ShouldBe("Properties");
        ((bool?)contents[1]!["writable"]).ShouldBe(false);
    }

    [Fact]
    public void Duplicate_property_name_throws()
    {
        Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty)),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Empty))));
    }

    [Fact]
    public void Non_class_top_level_property_throws()
    {
        Should.Throw<DtdlInterfaceEmissionException>(() =>
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

        fields["text"].ShouldBe("string");
        fields["count"].ShouldBe("integer");
        fields["big"].ShouldBe("long");
        fields["ratio"].ShouldBe("double");
        fields["small"].ShouldBe("float");
        fields["flag"].ShouldBe("boolean");
        fields["byte"].ShouldBe("integer");
        fields["short"].ShouldBe("integer");
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
        enumSchema.ShouldNotBeNull();
        ((string?)enumSchema!["@type"]).ShouldBe("Enum");
        ((string?)enumSchema["valueSchema"]).ShouldBe("string");

        var values = enumSchema["enumValues"]!.AsArray()
            .Cast<JsonObject>()
            .Select(o => (Name: (string)o["name"]!, EnumValue: (string)o["enumValue"]!))
            .ToArray();
        values.Select(v => v.Name).ShouldBe(new[] { "Red", "Green", "Blue" });
        values.Select(v => v.EnumValue).ShouldBe(new[] { "Red", "Green", "Blue" });
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

        values["BigEndian"].ShouldBe("big-endian");
        values["LittleEndian"].ShouldBe("little-endian");
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
        colorSchemas.Length.ShouldBe(1);
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
        ((string?)childField["schema"]).ShouldBe("dtmi:test:Demo:Test:Sample:Nested;1");

        var nestedObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Nested;1");
        nestedObj.ShouldNotBeNull();
        ((string?)nestedObj!["@type"]).ShouldBe("Object");
        var nestedFields = nestedObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!, f => (string?)f["schema"]);
        nestedFields["label"].ShouldBe("string");
        nestedFields["count"].ShouldBe("integer");
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
        nestedSchemas.Length.ShouldBe(1);
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
        arrSchema.ShouldNotBeNull();
        ((string?)arrSchema!["@type"]).ShouldBe("Array");
        ((string?)arrSchema["elementSchema"]).ShouldBe("string");
    }

    [Fact]
    public void Csharp_array_treated_same_as_list()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithArrays)));

        var arrSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:IndicesList;1");
        arrSchema.ShouldNotBeNull();
        ((string?)arrSchema!["elementSchema"]).ShouldBe("integer");
    }

    [Fact]
    public void Array_of_enum_references_enum_dtmi_as_element_schema()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithArrays)));

        var arrSchema = FindSchema(iface, "dtmi:test:Demo:Test:Sample:HuesList;1");
        arrSchema.ShouldNotBeNull();
        ((string?)arrSchema!["elementSchema"]).ShouldBe("dtmi:test:Demo:Test:Sample:Color;1");
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
        mapSchema.ShouldNotBeNull();
        ((string?)mapSchema!["@type"]).ShouldBe("Map");
        ((string?)mapSchema["mapKey"]!["schema"]).ShouldBe("string");
        ((string?)mapSchema["mapValue"]!["schema"]).ShouldBe("integer");
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

        names.ShouldContain("myField");
        names.ShouldContain("custom_name");
        names.ShouldNotContain("override");
        names.ShouldNotContain("Override");
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
        windowDesc.ShouldContain("Sliding window size");
        windowDesc.ShouldContain("range 1..100");
        windowDesc.ShouldContain("default 5");

        var codeDesc = (string?)byName["code"]["description"] ?? "";
        codeDesc.ShouldContain("required");
        codeDesc.ShouldContain(@"pattern ^\d+$");

        var nameDesc = (string?)byName["name"]["description"] ?? "";
        nameDesc.ShouldContain("length 3..20");
    }

    [Fact]
    public void Field_with_no_annotations_has_no_description_key()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(PrimitiveBag)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var firstField = paramsObj["fields"]!.AsArray()[0]!.AsObject();
        firstField.ContainsKey("description").ShouldBeFalse();
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
        var ex = Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithDateTime))));
        ex.Message.ShouldContain("DateTime");
    }

    [Fact]
    public void Guid_field_throws()
    {
        var ex = Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithGuid))));
        ex.Message.ShouldContain("Guid");
    }

    [Fact]
    public void Abstract_field_type_throws()
    {
        var ex = Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithStream))));
        ex.Message.ShouldContain("polymorphism", Case.Insensitive);
    }

    [Fact]
    public void Interface_field_type_throws()
    {
        Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithInterface))));
    }

    [Fact]
    public void Recursive_poco_throws()
    {
        var ex = Should.Throw<DtdlInterfaceEmissionException>(() =>
            DtdlInterfaceEmitter.Emit(
                DefaultOpts(),
                new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(Cycle))));
        ex.Message.ShouldContain("Circular");
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

        byName["maybe"].ShouldBe("integer");
        byName["optColor"].ShouldBe("dtmi:test:Demo:Test:Sample:Color;1");
    }

    private class WithDisplay
    {
        [Display(Name = "Pretty Label")]
        public int Value { get; init; }
    }

    [Fact]
    public void Display_attribute_emits_field_display_name()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            DefaultOpts(),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(WithDisplay)));

        var paramsObj = FindSchema(iface, "dtmi:test:Demo:Test:Sample:Parameters;1")!;
        var field = paramsObj["fields"]!.AsArray()[0]!.AsObject();
        ((string?)field["displayName"]).ShouldBe("Pretty Label");
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

        FindSchema(iface, "dtmi:test:Demo:Device:TcpModbus:Communication;1").ShouldNotBeNull();
        FindSchema(iface, "dtmi:test:Demo:Device:TcpModbus:Properties;1").ShouldNotBeNull();

        var contents = iface["contents"]!.AsArray();
        contents.Count.ShouldBe(2);
        ((string?)contents[0]!["schema"]).ShouldBe("dtmi:test:Demo:Device:TcpModbus:Communication;1");
        ((string?)contents[1]!["schema"]).ShouldBe("dtmi:test:Demo:Device:TcpModbus:Properties;1");
    }

    // ─── 13. Real-world fixtures ────────────────────────────────────────────
    // Use production POCOs (BatchReportParameters) to verify the emitter
    // handles realistic shapes end-to-end. Coupled to production by design:
    // if BatchReportParameters changes shape, these tests must be re-reviewed.

    [Fact]
    public void Real_world_BatchReportParameters_emits_expected_envelope()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            new DtdlInterfaceEmitter.Options(
                Prefix: "dtmi:advantech:EdgeSync:SubNode",
                Category: "Command",
                TypeName: "BatchReport",
                DisplayName: "Batch Report",
                Description: "Query historical telemetry within a time range and emit batched records."),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(BatchReportParameters)));

        ((string?)iface["@id"]).ShouldBe("dtmi:advantech:EdgeSync:SubNode:Command:BatchReport;1");
        ((string?)iface["displayName"]).ShouldBe("Batch Report");
        FindSchema(iface,
            "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Parameters;1").ShouldNotBeNull();
    }

    [Fact]
    public void Real_world_BatchReportParameters_emits_expected_fields()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            new DtdlInterfaceEmitter.Options(
                Prefix: "dtmi:advantech:EdgeSync:SubNode",
                Category: "Command",
                TypeName: "BatchReport"),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(BatchReportParameters)));

        var paramsObj = FindSchema(iface,
            "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Parameters;1")!;
        var fieldsByName = paramsObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!);

        // Nested record TimeRange → standalone Object referenced by DTMI
        ((string?)fieldsByName["timeRange"]["schema"])
            .ShouldBe("dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:TimeRange;1");

        // Nested record SensorFilter → standalone Object referenced by DTMI
        ((string?)fieldsByName["sensorFilter"]["schema"])
            .ShouldBe("dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:SensorFilter;1");

        // Range / Description / DefaultValue echoed into integer field's description
        var maxBatchSizeDesc = (string?)fieldsByName["maxBatchSize"]["description"] ?? "";
        maxBatchSizeDesc.ShouldContain("Maximum samples per batch");
        maxBatchSizeDesc.ShouldContain("range 1..100000");
        maxBatchSizeDesc.ShouldContain("default 10000");

        // Standalone TimeRange Object exists with long fields
        var timeRangeObj = FindSchema(iface,
            "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:TimeRange;1")!;
        var timeRangeFields = timeRangeObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .ToDictionary(f => (string)f["name"]!, f => (string?)f["schema"]);
        timeRangeFields["startTime"].ShouldBe("long");
        timeRangeFields["endTime"].ShouldBe("long");

        // SensorFilter has string[] Include / Exclude — Array DTMI references
        var sensorFilterObj = FindSchema(iface,
            "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:SensorFilter;1")!;
        var sfFields = sensorFilterObj["fields"]!.AsArray()
            .Cast<JsonObject>()
            .Select(f => (string)f["name"]!)
            .ToArray();
        sfFields.ShouldContain("include");
        sfFields.ShouldContain("exclude");
    }
}
