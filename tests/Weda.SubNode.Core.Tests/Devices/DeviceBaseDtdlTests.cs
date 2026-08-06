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
    public void SensorRef_CarriesNameAndDtmi_AsRequiredStringFields()
    {
        var fields = Items(DeviceBaseDtdl.GetInterface(), "schemas")
            .Single(s => s["@id"]?.GetValue<string>() == SensorRefDtmi)["fields"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToList();

        fields.Select(f => f["name"]!.GetValue<string>())
            .ShouldBe(new[] { "name", "dtmi" });

        foreach (var f in fields)
        {
            f["schema"]!.GetValue<string>().ShouldBe("string");
            f["required"]!.GetValue<bool>().ShouldBeTrue();
        }
    }
}
