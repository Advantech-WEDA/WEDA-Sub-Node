using CommandHandlerExample.Commands.GetDeviceProps.Models;

using Shouldly;

using Weda.SubNode.Core.Commands;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Regression tests for command auto-registration: CommandRegistry must be
/// able to scan this example assembly and emit a DTDL schema for every
/// handler — including props.get, whose command parameter type is a raw
/// Dictionary&lt;string, object&gt; and whose result data is a dynamic map.
/// A failure here means the app would crash at startup.
/// </summary>
public class CommandRegistrationTests
{
    [Fact]
    public void ScanAssembly_ShouldRegisterAllExampleCommands()
    {
        var registry = new CommandRegistry();

        registry.ScanAssembly(typeof(GetDevicePropsCommand).Assembly);

        registry.RegisteredCommands.ShouldContain("sensor.read");
        registry.RegisteredCommands.ShouldContain("tag.set");
        registry.RegisteredCommands.ShouldContain("props.get");
    }

    [Fact]
    public void ScanAssembly_PropsGet_ShouldEmitMapSchemas()
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(GetDevicePropsCommand).Assembly);

        var descriptor = registry.GetDescriptors().FirstOrDefault(d => d.Name == "props.get");

        descriptor.ShouldNotBeNull();
        descriptor.Schema.ShouldNotBeNull();
        var schemaJson = descriptor.Schema.ToJsonString();

        // The dictionary-typed request/response must surface as DTDL Map schemas
        schemaJson.ShouldContain("\"Map\"");
        schemaJson.ShouldContain("mapKey");
        schemaJson.ShouldContain("mapValue");
    }
}
