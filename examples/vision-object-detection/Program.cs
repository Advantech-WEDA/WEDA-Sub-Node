using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Host;

using VisionObjectDetection.Devices;
using VisionObjectDetection.Simulator;

var builder = WedaApplication.CreateDefaultBuilder(args);

// Ingest the Advantech YOLO object-detection container over MQTT.
// Config key must match "DeviceConfigs.VisionDetectionConfig" in devicecfg.json.
builder.AddDevice<VisionDetectionDevice>("VisionDetectionConfig");

// Optional: run a stand-in for the CV container so the pipeline can be verified
// without the real YOLO stack. Enable with:  dotnet run -- --simulate
if (args.Contains("--simulate"))
{
    builder.Services.AddHostedService(sp => new VisionSimulatorHostedService(
        brokerHost: "localhost",
        brokerPort: 1883,
        deviceId: "simdev01",
        logger: sp.GetRequiredService<ILogger<VisionSimulatorHostedService>>()));
}

var app = builder.Build();
await app.RunAsync();
