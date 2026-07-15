using System.Text.Json.Nodes;

using DTDLParser;
using DTDLParser.Models;

using Shouldly;

using Xunit;

namespace Weda.SubNode.Core.Tests.V12Feasibility;

// ─────────────────────────────────────────────────────────────────────────────
// Feasibility tests for the v1.2 ConfigurationUploadRequest payload against
// Microsoft DTDLParser (the same engine transceiver's IModelParser wraps).
//
// Covers ADO Parent #37095 (#37096–#37099). Each test synthesizes a payload-
// shape (wrapper Interface + refModels[]) and asserts the parser can:
//   2.1 #37096 — parse the batch and enumerate wrapper Telemetries
//   2.2 #37097 — resolve sensor-type Interface `extends Sensor:base` across
//                separate refModels entries (the cross-cache-entry case)
//   2.3 #37098 — surface [DeviceCmd] command Interfaces' Commands in the
//                parse cache (so transceiver's command lookup can find them
//                via refModels, NOT via sensor.Dtmi)
//   2.4 #37099 — accept ConfigStraints (minValue / maxValue / pattern / enum)
//                on Property contents and surface them as parsed metadata
// ─────────────────────────────────────────────────────────────────────────────

public class V12DtdlParserFeasibilityTests
{
    private const string SubNodeDeviceId = "54777925790076928";
    private const string WrapperDtmi = "dtmi:advantech:edgesync:subnode_a1b2c3d4;1";
    private const string TempTelemetryDtmi = "dtmi:autogen:temp:be10e40b;1";
    private const string SensorBaseDtmi = "dtmi:weda:sensor:base;1";
    private const string TcpModbusTempDtmi = "dtmi:weda:tcpmodbus:temperature;1";
    private const string ReportDataCmdDtmi = "dtmi:weda:cmd:reportdata;1";
    private const string UnitConvTransformDtmi = "dtmi:weda:transform:unitconversion;1";

    [Fact]
    public void Telemetry_WithUnitField_RejectedByDtdlParser_RegressionGuard()
    {
        // Regression guard: if someone re-introduces `unit` on a bare Telemetry,
        // this test fails so the change can't ship silently. `unit` is NOT
        // part of core DTDL v3 Telemetry — it lives only in QuantitativeTypes
        // extension under a co-typed Telemetry — and AllowUndefinedExtensions
        // does NOT relax property-level term validation (confirmed
        // experimentally; that flag only tolerates undefined extension
        // CONTEXT URIs, not undefined PROPERTIES).
        //
        // The unit travels in the upload payload via
        // deviceCapabilities.sensors[i].unit; see SensorDto.Unit.
        var wrapper = new JsonObject
        {
            ["@context"] = "dtmi:dtdl:context;3",
            ["@id"] = WrapperDtmi,
            ["@type"] = "Interface",
            ["displayName"] = "Hypothetical wrapper that re-introduces unit",
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["@id"] = TempTelemetryDtmi,
                    ["@type"] = "Telemetry",
                    ["name"] = "temperature_sensor",
                    ["schema"] = "double",
                    ["unit"] = "celsius"   // ← the smoking gun
                }
            }
        };
        var defaultParser = new ModelParser();

        var ex = Should.Throw<ParsingException>(() =>
            defaultParser.Parse(new[] { wrapper.ToJsonString() }));

        ex.Errors.ShouldContain(e =>
            e.ToString().Contains("'unit'") && e.ToString().Contains("undefined term"));
    }

    [Fact]
    public void RealisticAutogenInterface_NoUnit_ParsesCleanlyUnderDefaultParserOptions()
    {
        // Positive test that exercises the actual DtdlGenerator emit path —
        // build a Sensor, run it through GenerateInterface, then feed the
        // resulting JSON to DTDLParser with default ParsingOptions (exactly
        // what transceiver's FileSystemDtmiResolver.ParsingOptions does).
        // No `unit` should be emitted (Fix A); the parse must succeed.
        var sensor = new Abstractions.Telemetry.Sensor
        {
            Name = "temperature_sensor",
            SensorGroup = Abstractions.Telemetry.SensorGroup.TEMP,
            Report = new Abstractions.Telemetry.SensorReport { Unit = "celsius" },
            SensorInfo = new Abstractions.Telemetry.SensorInfo
            {
                DisplayName = "Temperature Sensor",
                Description = "Sensor for temperature sampling",
                Schema = "double"
            }
        };
        Abstractions.DigitalTwin.DtdlGenerator.PopulateSensorDtmis([sensor]);
        var autogenInterface = Abstractions.DigitalTwin.DtdlGenerator.GenerateInterface(
            "TestDevice", [sensor]);

        var json = System.Text.Json.JsonSerializer.Serialize(autogenInterface,
            new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        // Smoke check the emit contract: no "unit" key in the JSON at all.
        json.ShouldNotContain("\"unit\"");

        var parser = new ModelParser();
        var models = parser.Parse(new[] { json });

        models.ShouldNotBeEmpty();
        var iface = models.Values.OfType<DTInterfaceInfo>().Single();
        iface.Contents.Keys.ShouldContain("temperature_sensor");
    }

    [Fact]
    public void DeviceModelAlone_NoRefModels_ParsesCleanly()
    {
        // Transceiver's UpdateDtdlModelHandler pushes models into the parser
        // cache one at a time via UpdateCacheAsync. This test pins that the
        // wrapper Interface (DeviceModel) can be parsed in isolation — no
        // refModels required for it to be self-consistent.
        var wrapper = BuildWrapperWithSingleTelemetry();
        var parser = new ModelParser();

        var models = parser.Parse(new[] { wrapper.ToJsonString() });

        models.Keys.ShouldContain(new Dtmi(WrapperDtmi));
        var wrapperInfo = models[new Dtmi(WrapperDtmi)].ShouldBeOfType<DTInterfaceInfo>();
        wrapperInfo.Contents.Keys.ShouldContain("temperature_sensor");
    }

    [Fact]
    public void BatchParse_WrapperPlusRefModels_Succeeds_AndWrapperTelemetriesEnumerable()
    {
        var wrapper = BuildWrapperWithSingleTelemetry();
        var sensorBase = BuildSensorBase();
        var parser = new ModelParser();

        var models = parser.Parse(new[]
        {
            wrapper.ToJsonString(),
            sensorBase.ToJsonString()
        });

        models.Keys.ShouldContain(new Dtmi(WrapperDtmi));
        var wrapperInfo = models[new Dtmi(WrapperDtmi)].ShouldBeOfType<DTInterfaceInfo>();

        // Contents are keyed by local name (NOT full DTMI); the DTMI is on the
        // DTContentInfo.Id field. This matches DTDL spec semantics — content
        // identifiers are scoped within the Interface.
        wrapperInfo.Contents.Keys.ShouldContain("temperature_sensor");
        var telemetry = wrapperInfo.Contents["temperature_sensor"]
            .ShouldBeOfType<DTTelemetryInfo>();

        telemetry.Id.AbsoluteUri.ShouldBe(TempTelemetryDtmi);
        // DTDLParser exposes primitive schemas via DTPrimitiveSchemaInfo subtypes;
        // for "double" it's DTDoubleInfo. EntityKind enum check is the most
        // version-portable assertion.
        telemetry.Schema.EntityKind.ShouldBe(DTEntityKind.Double);
    }

    [Fact]
    public void SensorTypeInterface_Extends_SensorBase_ResolvesAcrossRefModelsEntries()
    {
        // Mismatch #3 from the refactor plan: UpdateDtdlModelHandler currently
        // calls UpdateCacheAsync per refModel one at a time. If ParseAsync can
        // handle the whole batch cooperatively we're fine; if not, the cloud
        // needs to switch to batch. This test pins the "batch works" path.
        var wrapper = BuildWrapperWithSingleTelemetry();
        var sensorBase = BuildSensorBase();
        var tcpModbusTemp = BuildTcpModbusTemperatureSensorType();
        var parser = new ModelParser();

        var models = parser.Parse(new[]
        {
            wrapper.ToJsonString(),
            sensorBase.ToJsonString(),
            tcpModbusTemp.ToJsonString()
        });

        models.Keys.ShouldContain(new Dtmi(TcpModbusTempDtmi));
        var typeInfo = models[new Dtmi(TcpModbusTempDtmi)]
            .ShouldBeOfType<DTInterfaceInfo>();

        typeInfo.Extends.ShouldNotBeEmpty();
        typeInfo.Extends.ShouldContain(base_ => base_.Id.AbsoluteUri == SensorBaseDtmi);

        // Inherited content (a "name" Property declared on Sensor:base) must
        // be visible on the sensor-type Interface too — that's the proof that
        // extends resolution actually happened.
        typeInfo.Contents.Keys.ShouldContain("name");
        typeInfo.Contents["name"].Id.AbsoluteUri.ShouldBe("dtmi:weda:sensor:base:name;1");
    }

    [Fact]
    public void CommandInterface_InRefModels_HasCommandsEnumerable()
    {
        // Transceiver's *CommandValidationService walks DTInterfaceInfo.Commands.
        // The v1.2 design puts command Interfaces in refModels[] (not bound to
        // sensor.Dtmi). This test proves the lookup path is reachable.
        var wrapper = BuildWrapperWithSingleTelemetry();
        var commandInterface = BuildReportDataCommandInterface();
        var parser = new ModelParser();

        var models = parser.Parse(new[]
        {
            wrapper.ToJsonString(),
            commandInterface.ToJsonString()
        });

        models.Keys.ShouldContain(new Dtmi(ReportDataCmdDtmi));
        var cmdInterface = models[new Dtmi(ReportDataCmdDtmi)]
            .ShouldBeOfType<DTInterfaceInfo>();

        cmdInterface.Commands.Keys.ShouldContain("report");
        var cmd = cmdInterface.Commands["report"];
        cmd.Id.AbsoluteUri.ShouldBe("dtmi:weda:cmd:reportdata:report;1");
    }

    [Fact]
    public void ConfigStraints_EnumSchema_OnProperty_SurvivesParseAndExposesEnumValues()
    {
        // ConfigStraints scope-of-work:
        //   ✅ Enum schema  — native DTDL v3 Property support (this test).
        //   ✅ Object/Field — native; constraints would be expressed via field
        //                     types or sub-Enum schemas.
        //   ✅ Length / regex on string Property — via DTDL v3 stringSchema
        //                     attributes (covered separately if needed).
        //   ❌ minValue/maxValue on numeric Property — NOT in core DTDL v3.
        //      DTDLParser 1.1.3 rejects with "undefined term" when emitted
        //      directly. NumericValue extension applies to quantitativeTypes
        //      (Temperature, Velocity, …), not to generic min/max constraints.
        //      → Follow-up #37104: emitter must either (a) wrap in Object schema
        //      and constrain via field shapes; (b) use SupplementalProperties
        //      with a private namespace the cloud-side teaches DTDLParser
        //      about; or (c) drop min/max from refModels and validate edge-side.
        var wrapper = BuildWrapperWithSingleTelemetry();
        var transform = BuildUnitConversionWithEnumConstraint();
        var parser = new ModelParser();

        IReadOnlyDictionary<Dtmi, DTEntityInfo> models;
        try
        {
            models = parser.Parse(new[]
            {
                wrapper.ToJsonString(),
                transform.ToJsonString()
            });
        }
        catch (ParsingException pex)
        {
            // Surface every error so the failure message says exactly what
            // DTDLParser is rejecting — turns this into a diagnostic test.
            var errs = string.Join("\n  - ", pex.Errors.Select(e => e.ToString()));
            throw new Xunit.Sdk.XunitException(
                $"DTDLParser rejected the ConfigStraints fixture:\n  - {errs}\n\nFixture JSON:\n{transform.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
        }

        models.Keys.ShouldContain(new Dtmi(UnitConvTransformDtmi));
        var transformInfo = models[new Dtmi(UnitConvTransformDtmi)]
            .ShouldBeOfType<DTInterfaceInfo>();

        const string modeId = "dtmi:weda:transform:unitconversion:mode;1";
        transformInfo.Contents.Keys.ShouldContain("mode");
        var modeProperty = transformInfo.Contents["mode"]
            .ShouldBeOfType<DTPropertyInfo>();
        modeProperty.Id.AbsoluteUri.ShouldBe(modeId);

        // The enum constraint must survive parse so the cloud validator can
        // reject desired-state values outside the allowed set.
        var enumSchema = modeProperty.Schema.ShouldBeOfType<DTEnumInfo>();
        enumSchema.ValueSchema.EntityKind.ShouldBe(DTEntityKind.String);
        enumSchema.EnumValues.Select(v => v.Name)
            .ShouldBe(new[] { "celsiusToFahrenheit", "fahrenheitToCelsius" }, ignoreOrder: true);
    }

    // ─── fixture builders ───────────────────────────────────────────────────

    private static JsonObject BuildWrapperWithSingleTelemetry() => new()
    {
        ["@context"] = "dtmi:dtdl:context;3",
        ["@id"] = WrapperDtmi,
        ["@type"] = "Interface",
        ["displayName"] = "MyFirstApplication",
        ["description"] = "feasibility fixture",
        ["contents"] = new JsonArray
        {
            new JsonObject
            {
                ["@id"] = TempTelemetryDtmi,
                ["@type"] = "Telemetry",
                ["name"] = "temperature_sensor",
                ["displayName"] = "Temperature Sensor",
                ["schema"] = "double"
            }
        }
    };

    private static JsonObject BuildSensorBase() => new()
    {
        ["@context"] = "dtmi:dtdl:context;3",
        ["@id"] = SensorBaseDtmi,
        ["@type"] = "Interface",
        ["displayName"] = "Sensor",
        ["contents"] = new JsonArray
        {
            new JsonObject
            {
                ["@id"] = "dtmi:weda:sensor:base:name;1",
                ["@type"] = "Property",
                ["name"] = "name",
                ["schema"] = "string"
            }
        }
    };

    private static JsonObject BuildTcpModbusTemperatureSensorType() => new()
    {
        ["@context"] = "dtmi:dtdl:context;3",
        ["@id"] = TcpModbusTempDtmi,
        ["@type"] = "Interface",
        ["extends"] = SensorBaseDtmi,
        ["displayName"] = "TcpModbusTemperatureSensor"
    };

    private static JsonObject BuildReportDataCommandInterface() => new()
    {
        ["@context"] = "dtmi:dtdl:context;3",
        ["@id"] = ReportDataCmdDtmi,
        ["@type"] = "Interface",
        ["displayName"] = "ReportData",
        ["contents"] = new JsonArray
        {
            new JsonObject
            {
                ["@id"] = "dtmi:weda:cmd:reportdata:report;1",
                ["@type"] = "Command",
                ["name"] = "report",
                ["request"] = new JsonObject
                {
                    ["name"] = "parameters",
                    ["schema"] = new JsonObject
                    {
                        ["@type"] = "Object",
                        ["fields"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["name"] = "sensorId",
                                ["schema"] = "string"
                            }
                        }
                    }
                },
                ["response"] = new JsonObject
                {
                    ["name"] = "result",
                    ["schema"] = "integer"
                }
            }
        }
    };

    private static JsonObject BuildUnitConversionWithEnumConstraint() => new()
    {
        ["@context"] = "dtmi:dtdl:context;3",
        ["@id"] = UnitConvTransformDtmi,
        ["@type"] = "Interface",
        ["displayName"] = "UnitConversion",
        ["contents"] = new JsonArray
        {
            new JsonObject
            {
                ["@id"] = "dtmi:weda:transform:unitconversion:mode;1",
                ["@type"] = "Property",
                ["name"] = "mode",
                ["schema"] = new JsonObject
                {
                    ["@type"] = "Enum",
                    ["valueSchema"] = "string",
                    ["enumValues"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "celsiusToFahrenheit",
                            ["enumValue"] = "CelsiusToFahrenheit"
                        },
                        new JsonObject
                        {
                            ["name"] = "fahrenheitToCelsius",
                            ["enumValue"] = "FahrenheitToCelsius"
                        }
                    }
                }
            }
        }
    };
}
