using System.Text.Json;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

public class TelemetryMeasureDtoTests
{
    [Fact]
    public void From_NumericValue_PreservesAsIs()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = 25.5, Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        dto.Measures[0].Value.ShouldBe(25.5);
    }

    [Fact]
    public void From_Plainstring_PreserveAsIs()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = "hello world", Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        dto.Measures[0].Value.ShouldBe("hello world"); 
    }

    [Fact]
    public void From_JsonObjectString_ConvertsToJsonElement()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = "{\"alarm\":true,\"code\":42}", Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        dto.Measures[0].Value.GetType().ShouldBe(typeof(JsonElement));
        var element = (JsonElement)dto.Measures[0].Value;
        element.GetProperty("alarm").GetBoolean().ShouldBeTrue();
        element.GetProperty("code").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void From_NestedJsonObject_PreservesStructure()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            evt = new
            {
                type = "alarm",
                details = new
                {
                    severity = "high",
                }   
            },
            tags = new object[]
            {
                "critical"
            }
        });
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = json, Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        var element = (JsonElement)dto.Measures[0].Value;
        element.GetProperty("evt").GetProperty("type").GetString().ShouldBe("alarm");
        element.GetProperty("evt").GetProperty("details").GetProperty("severity").GetString().ShouldBe("high");
        element.GetProperty("tags")[0].GetString().ShouldBe("critical");
    }

    [Fact]
    public void From_JsonStringWithLeadingWhitespace_ConvertsToJsonElement()
    {
        // Arrange 
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = "    {\"a\":1}", Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        var element = (JsonElement)dto.Measures[0].Value;
        element.GetType().ShouldBe(typeof(JsonElement));
        element.GetProperty("a").GetInt32().ShouldBe(1);
    }

    [Fact]
    public void From_NestedJsonString_PreservesOriginal()
    {
        // Arrange
        string text = "{invalid json";
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = text, Timestamp = DateTime.UtcNow.Millisecond }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        dto.Measures[0].Value.ShouldBe(text);
    }
}