using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Transforms;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

/// <summary>
/// End-to-end verification that startup-time discovery wires the right set of
/// transforms / DSP filters / commands into descriptors that cloud will receive
/// inside <c>SubNodeCapabilitiesDto</c>.
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

        // Descriptors carry descriptions sourced from `static Description`.
        foreach (var d in descriptors)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Description),
                $"Transform '{d.TypeName}' is missing a Description.");
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

        // Each command must surface both parameter and response schemas.
        foreach (var d in descriptors)
        {
            Assert.NotNull(d.ParameterSchema);
            Assert.NotNull(d.ResponseSchema);
            Assert.Equal("object", (string?)d.ParameterSchema.Root["type"]);
        }
    }

    [Fact]
    public void Command_set_digital_output_descriptor_has_required_outputs_schema()
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);

        var descriptor = registry.GetDescriptors().First(d => d.Name == "do.set");

        var props = descriptor.ParameterSchema.Root["properties"]!;
        Assert.NotNull(props["outputs"]);
        Assert.Equal("array", (string?)props["outputs"]!["type"]);

        var required = descriptor.ParameterSchema.Root["required"]?.AsArray()
            .Select(n => (string?)n).ToHashSet();
        Assert.NotNull(required);
        Assert.Contains("outputs", required);
    }
}
