using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

using Shouldly;

using Weda.SubNode.Abstractions.Devices;

using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Shape guards for the hand-authored <c>device:base</c> DTDL Interface. DTDL
/// well-formedness (extends resolution + ConfigConstraint) is covered by the
/// integration-level <c>CapabilityUploadDtdlE2ETests</c> via the real
/// <c>WedaDtValidator</c>; these tests pin the JSON structure so the
/// device-to-sensor <c>Sensors</c> slot cannot regress silently.
/// </summary>
public class DeviceBaseDtdlTests
{
    private const string SensorRefDtmi  = "dtmi:advantech:weda:device:base:SensorRef;1";
    private const string SensorListDtmi = "dtmi:advantech:weda:device:base:SensorList;1";

    private static IEnumerable<JsonObject> Items(JsonObject iface, string key) =>
        (iface[key] as JsonArray ?? new JsonArray()).OfType<JsonObject>();

    [Fact]
    public void GetInterface_Contents_ExposeEnabledAndSensors()
    {
        var names = Items(DeviceBaseDtdl.GetInterface(), "contents")
            .Select(c => c["name"]?.GetValue<string>())
            .ToList();

        names.ShouldContain("Enabled");
        names.ShouldContain("Sensors");
    }

    [Fact]
    public void SensorsProperty_IsWritableConfigConstraint_PointingToSensorList()
    {
        var sensors = Items(DeviceBaseDtdl.GetInterface(), "contents")
            .Single(c => c["name"]?.GetValue<string>() == "Sensors");

        (sensors["@type"] as JsonArray)!.Select(t => t!.GetValue<string>())
            .ShouldBe(new[] { "Property", "ConfigConstraint" });
        sensors["schema"]!.GetValue<string>().ShouldBe(SensorListDtmi);
        sensors["writable"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Schemas_DefineSensorList_AsArrayOfSensorRef()
    {
        var schemas = Items(DeviceBaseDtdl.GetInterface(), "schemas").ToList();

        var list = schemas.Single(s => s["@id"]?.GetValue<string>() == SensorListDtmi);
        list["@type"]!.GetValue<string>().ShouldBe("Array");
        list["elementSchema"]!.GetValue<string>().ShouldBe(SensorRefDtmi);

        var sensorRef = schemas.Single(s => s["@id"]?.GetValue<string>() == SensorRefDtmi);
        sensorRef["@type"]!.GetValue<string>().ShouldBe("Object");
    }

    [Fact]
    public void SensorRef_CarriesFullSensorEnvelope_OnlyNameRequired()
    {
        var fields = Items(DeviceBaseDtdl.GetInterface(), "schemas")
            .Single(s => s["@id"]?.GetValue<string>() == SensorRefDtmi)["fields"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToList();

        fields.Select(f => f["name"]!.GetValue<string>())
            .ShouldBe(new[] { "name", "dtmi", "sensorGroup", "report", "record", "sensorInfo" });

        // Only name is required: the SubNode owns DTMI assignment and every other
        // envelope field is defaultable, so a cloud-written Sensors value carrying a
        // bare {name} entry must still validate.
        foreach (var f in fields)
        {
            var required = f["required"]!.GetValue<bool>();
            required.ShouldBe(f["name"]!.GetValue<string>() == "name");
        }

        // Envelope fields use device-scoped schema copies (shape-shared with
        // sensor:base via SensorEnvelopeSchemas).
        Schema(fields, "name").ShouldBe("string");
        Schema(fields, "dtmi").ShouldBe("string");
        Schema(fields, "sensorGroup").ShouldBe("string");
        Schema(fields, "report").ShouldBe("dtmi:advantech:weda:device:base:Report;1");
        Schema(fields, "record").ShouldBe("dtmi:advantech:weda:device:base:Record;1");
        Schema(fields, "sensorInfo").ShouldBe("dtmi:advantech:weda:device:base:SensorInfo;1");
    }

    [Fact]
    public void Interface_IsSelfContained_EveryReferencedSchemaIsDefinedLocally()
    {
        // The shadow validator parses each Interface with only its extends closure,
        // so every dtmi-valued schema reference must resolve inside this Interface.
        var iface = DeviceBaseDtdl.GetInterface();
        var schemas = Items(iface, "schemas");

        var definedIds = schemas
            .Select(s => s["@id"]!.GetValue<string>())
            .ToHashSet();

        var referencedIds = schemas
            .SelectMany(s => s["fields"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            .Select(f => f["schema"]!.GetValue<string>())
            .Concat(schemas.Select(s => s["elementSchema"]?.GetValue<string>()).OfType<string>())
            .Concat(Items(iface, "contents").Select(c => c["schema"]!.GetValue<string>()))
            .Where(s => s.StartsWith("dtmi:", StringComparison.Ordinal));

        foreach (var id in referencedIds)
            definedIds.ShouldContain(id);
    }

    private static string Schema(IEnumerable<JsonObject> fields, string fieldName)
        => fields.Single(f => f["name"]!.GetValue<string>() == fieldName)["schema"]!.GetValue<string>();
}
