using System.Text.Json;
using System.Text.Json.Serialization;

using DTDLParser;

using Shouldly;

using StockMonitor.Protocols;

using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace StockMonitor.Tests;

/// <summary>
/// The point of one-sensor-per-metric is that the auto-generated model can describe each reading
/// truthfully. These tests build the Interface from the shipped configuration and check it.
/// </summary>
public class GeneratedDtdlTests
{
    private static List<Sensor> SensorsFromConfig()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../examples/stock-monitor/devicecfg.json"));

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement
            .GetProperty("DeviceConfigs").GetProperty("StockMonitorConfig").GetProperty("Sensors")
            .EnumerateArray()
            .Select(s => new Sensor
            {
                Name = s.GetProperty("Name").GetString()!,
                SensorGroup = Enum.Parse<SensorGroup>(s.GetProperty("SensorGroup").GetString()!),
                SensorInfo = new SensorInfo
                {
                    Schema = s.GetProperty("SensorInfo").GetProperty("Schema").GetString()!,
                    DisplayName = s.GetProperty("SensorInfo").GetProperty("DisplayName").GetString()!,
                },
            })
            .ToList();
    }

    [Fact]
    public void EverySensorIsModelledAsItsOwnTelemetry()
    {
        var sensors = SensorsFromConfig();

        var dtdlInterface = DtdlGenerator.GenerateInterface("StockMonitorConfig", sensors);

        dtdlInterface.Contents.Count.ShouldBe(sensors.Count);
        dtdlInterface.Contents.Select(c => c.Id).Distinct().Count().ShouldBe(sensors.Count);
    }

    [Fact]
    public void VolumeIsModelledAsLongAndPricesAsDouble()
    {
        var sensors = SensorsFromConfig();

        var dtdlInterface = DtdlGenerator.GenerateInterface("StockMonitorConfig", sensors);

        var bySchema = dtdlInterface.Contents.ToDictionary(c => c.Name!, c => c.Schema);

        bySchema["stock_2395_volume"].ShouldBe(StockMetricCatalog.SchemaFor("Volume"));
        bySchema["stock_2395_current"].ShouldBe(StockMetricCatalog.SchemaFor("Current"));
    }

    [Fact]
    public void TheGeneratedInterfaceParses()
    {
        // A model the parser rejects never reaches the cloud, so generating one is not enough.
        var sensors = SensorsFromConfig();
        DtdlGenerator.PopulateSensorDtmis(sensors, deviceKey: "StockMonitorConfig");

        var dtdlInterface = DtdlGenerator.GenerateInterface("StockMonitorConfig", sensors);

        // Same options the upload path uses: unset DTDL properties are omitted, not emitted as
        // null, which the parser rejects.
        var json = JsonSerializer.Serialize(
            dtdlInterface,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

        try
        {
            new ModelParser().Parse(json);
        }
        catch (ParsingException ex)
        {
            throw new Xunit.Sdk.XunitException(
                string.Join("\n", ex.Errors.Take(4).Select(e => e.Message)));
        }
    }
}
