using System.Text.Json.Nodes;

using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Transforms;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

/// <summary>
/// End-to-end verification that startup-time discovery wires the right set of
/// transforms / DSP filters / commands into descriptors that cloud will receive
/// inside <c>DeviceCapDto</c>.
/// </summary>
public class CapabilityUploadIntegrationTests
{
    [Fact]
    public void Transform_descriptors_cover_all_in_tree_transforms()
    {
        var descriptors = TransformFactory.GetDescriptors();

        Assert.Equal(3, descriptors.Count);
        var names = descriptors.Select(d => d.TypeName).ToHashSet();
        Assert.Equal(new[] { "calibration", "chunking", "unitconversion" }.OrderBy(n => n),
            names.OrderBy(n => n));

        foreach (var d in descriptors)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Description),
                $"Transform '{d.TypeName}' is missing a Description.");
            AssertIsDtdlInterface(d.ParameterSchema, $"dtmi:advantech:weda:transform:{d.TypeName};1");
        }
    }

    [Fact]
    public void DspFilter_descriptors_cover_all_in_tree_filters()
    {
        var descriptors = DspFilterFactory.GetDescriptors();

        Assert.Equal(3, descriptors.Count);
        var names = descriptors.Select(d => d.TypeName).ToHashSet();
        Assert.Equal(new[] { "kalman", "movingaverage", "relu" }.OrderBy(n => n),
            names.OrderBy(n => n));

        foreach (var d in descriptors)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Description),
                $"DSP filter '{d.TypeName}' is missing a Description.");
            AssertIsDtdlInterface(d.ParameterSchema, $"dtmi:advantech:weda:dspfilter:{d.TypeName};1");
        }
    }

    [Fact]
    public void Command_descriptors_cover_all_in_tree_commands()
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);
        var descriptors = registry.GetDescriptors();

        var names = descriptors.Select(d => d.Name).ToHashSet();
        Assert.Contains("report.historical", names);
        Assert.Contains("report.data", names);
        Assert.Contains("do.set", names);

        foreach (var d in descriptors)
        {
            Assert.NotNull(d.Schema);

            // Mirror WedaDtdlEmitter.Sanitize: replace illegal DTMI chars with '_', preserve case.
            var sanitized = d.Name.Replace('.', '_').Replace('-', '_').Replace(' ', '_');
            AssertIsDtdlInterface(d.Schema, $"dtmi:advantech:weda:command:{sanitized};1");
        }
    }

    [Fact]
    public void Command_set_digital_output_descriptor_has_required_outputs_field()
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);

        var descriptor = registry.GetDescriptors().First(d => d.Name == "do.set");

        // DTDL shape: contents[0] is the Command entry; its request.schema
        // points to a Parameters Object in schemas[].
        var contents = descriptor.Schema["contents"]!.AsArray();
        var commandEntry = contents.OfType<JsonObject>()
            .First(c => (string?)c["@type"] == "Command");
        var requestRef = (string?)commandEntry["request"]!["schema"];
        Assert.NotNull(requestRef);

        var objectSchema = descriptor.Schema["schemas"]!.AsArray()
            .OfType<JsonObject>()
            .First(s => (string?)s["@id"] == requestRef);

        var fields = objectSchema["fields"]!.AsArray();
        var outputsField = fields.OfType<JsonObject>()
            .FirstOrDefault(f => (string?)f["name"] == "outputs");
        Assert.NotNull(outputsField);

        // outputs is bound to an Array schema (DTMI ending in :outputsList;1).
        var outputsSchemaRef = (string?)outputsField["schema"];
        Assert.NotNull(outputsSchemaRef);
        var outputsSchema = descriptor.Schema["schemas"]!.AsArray()
            .OfType<JsonObject>()
            .First(s => (string?)s["@id"] == outputsSchemaRef);
        Assert.Equal("Array", (string?)outputsSchema["@type"]);

        // [Required] on the field surfaces as the ConfigConstraint "required" extension field.
        Assert.True((bool?)outputsField["required"] ?? false,
            "outputs field must be marked required via ConfigConstraint extension.");
    }

    private static void AssertIsDtdlInterface(JsonObject schema, string expectedId)
    {
        Assert.Equal("Interface", (string?)schema["@type"]);
        Assert.Equal(expectedId, (string?)schema["@id"]);
        Assert.NotNull(schema["@context"]);
        Assert.NotNull(schema["contents"]);
        Assert.NotNull(schema["schemas"]);
    }
}
