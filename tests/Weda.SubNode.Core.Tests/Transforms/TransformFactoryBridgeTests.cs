using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Transforms;

using Xunit;

namespace Weda.SubNode.Core.Tests.Transforms;

/// <summary>
/// Validates the Dict → TParameter bridge inside TransformFactory using the
/// real production registrations (UnitConversion, Calibration, Chunking).
/// </summary>
public class TransformFactoryBridgeTests
{
    [Fact]
    public void RegisteredTypes_includes_all_in_tree_transforms()
    {
        Assert.Contains("unitconversion", TransformFactory.RegisteredTypes,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains("calibration", TransformFactory.RegisteredTypes,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains("chunking", TransformFactory.RegisteredTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDescriptors_returns_descriptor_per_registered_transform()
    {
        var descriptors = TransformFactory.GetDescriptors();

        Assert.Contains(descriptors, d => d.TypeName == "unitconversion");
        Assert.Contains(descriptors, d => d.TypeName == "calibration");
        Assert.Contains(descriptors, d => d.TypeName == "chunking");

        // Schemas are non-null DTDL Interfaces.
        foreach (var d in descriptors)
        {
            Assert.NotNull(d.ParameterSchema);
            Assert.Equal("Interface", (string?)d.ParameterSchema["@type"]);
            Assert.Equal($"dtmi:advantech:weda:transform:{d.TypeName};1",
                (string?)d.ParameterSchema["@id"]);
        }
    }

    [Fact]
    public void CreateTransform_with_camelCase_dict_keys_deserializes_to_TParameter()
    {
        var config = new TransformConfig
        {
            Type = "unitconversion",
            Enabled = true,
            Parameters = new Dictionary<string, object>
            {
                ["fromUnit"] = "C",
                ["toUnit"] = "F",
            },
        };

        var transform = TransformFactory.CreateTransform(config);

        Assert.NotNull(transform);
        Assert.IsType<UnitConversionTransform>(transform);
    }

    [Fact]
    public void CreateTransform_missing_required_parameter_throws_validation_exception()
    {
        var config = new TransformConfig
        {
            Type = "unitconversion",
            Enabled = true,
            Parameters = new Dictionary<string, object>
            {
                ["fromUnit"] = "C",
                // toUnit intentionally missing — [Required] should fail
            },
        };

        var ex = Assert.Throws<ValidationException>(() => TransformFactory.CreateTransform(config));
        Assert.Contains("unitconversion", ex.Message);
    }

    [Fact]
    public void CreateTransform_out_of_range_parameter_throws_validation_exception()
    {
        var config = new TransformConfig
        {
            Type = "chunking",
            Enabled = true,
            Parameters = new Dictionary<string, object>
            {
                ["chunkSize"] = 100, // below [Range] minimum of 1024
            },
        };

        var ex = Assert.Throws<ValidationException>(() => TransformFactory.CreateTransform(config));
        Assert.Contains("chunking", ex.Message);
    }

    [Fact]
    public void CreateTransform_disabled_returns_null_without_validating()
    {
        var config = new TransformConfig
        {
            Type = "unitconversion",
            Enabled = false,
            // Invalid parameters but should never reach the bridge.
            Parameters = new Dictionary<string, object>(),
        };

        Assert.Null(TransformFactory.CreateTransform(config));
    }

    [Fact]
    public void CreateTransform_unregistered_type_throws_NotSupportedException()
    {
        var config = new TransformConfig
        {
            Type = "definitely-not-registered",
            Enabled = true,
            Parameters = new Dictionary<string, object>(),
        };

        Assert.Throws<NotSupportedException>(() => TransformFactory.CreateTransform(config));
    }

    [Fact]
    public void Descriptors_carry_cached_schema_from_emitter()
    {
        var descriptor = TransformFactory.GetDescriptors()
            .First(d => d.TypeName == "unitconversion");

        // DTDL navigation: find the inner Object schema that defines the POCO's fields.
        var paramsEntry = descriptor.ParameterSchema["contents"]!.AsArray()
            .OfType<JsonObject>()
            .First(c => (string?)c["name"] == "parameters");
        var schemaRef = (string?)paramsEntry["schema"];
        var paramObject = descriptor.ParameterSchema["schemas"]!.AsArray()
            .OfType<JsonObject>()
            .First(s => (string?)s["@id"] == schemaRef);

        var fields = paramObject["fields"]!.AsArray().OfType<JsonObject>().ToList();
        var fromUnit = fields.First(f => (string?)f["name"] == "fromUnit");
        var toUnit = fields.First(f => (string?)f["name"] == "toUnit");

        // [Required] surfaces as the ConfigConstraint "required" extension field.
        Assert.True((bool?)fromUnit["required"] ?? false);
        Assert.True((bool?)toUnit["required"] ?? false);
    }
}
