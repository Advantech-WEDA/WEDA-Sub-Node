using CfxEndpointExample;

using Serilog;

using Weda.SubNode.Host;

try
{
    var builder = WedaApplication.CreateDefaultBuilder(args);

    // Cloud target is configuration-driven so the same image runs either way:
    //   customcfg.json "UseMockCloud": true  -> log telemetry locally, no WedaNode needed
    //   customcfg.json "UseMockCloud": false -> publish to the WedaNode in systemcfg.json
    // The WedaNode password is supplied by the SystemConfig__WedaNode__Password environment
    // variable rather than committed to systemcfg.json.
    var useMockCloud =
        bool.TryParse(builder.Configuration["CustomConfig:UseMockCloud"], out var mock) && mock;

    if (useMockCloud)
    {
        Log.Information("Cloud: mock (CustomConfig:UseMockCloud=true)");
        builder.UseMockCloud();
    }
    else
    {
        Log.Information(
            "Cloud: WedaNode at {Url}",
            builder.Configuration["SystemConfig:WedaNode:Url"]);
    }

    builder.AddDevice<MyCfxEndpointDevice>("CfxEndpoint");

    var app = builder.Build();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
