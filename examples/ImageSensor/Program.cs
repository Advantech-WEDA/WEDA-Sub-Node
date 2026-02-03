using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ImageSensor;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Mqtt;

var builder = WedaApplication.CreateDefaultBuilder(args);

builder.AddDevice<ImageSensorDevice>("ImageSensorConfig");

// Register MQTT Image Simulator as hosted service
builder.Services.AddHostedService(sp =>
{
    var config = new MqttImageSimulatorConfiguration
    {
        BrokerHost = "localhost",
        BrokerPort = 1883,
        Topic = "sensor/image/mnist",
        IntervalSeconds = 3
    };
    var logger = sp.GetRequiredService<ILogger<MqttImageSimulator>>();
    return new MqttImageSimulatorHostedService(config, logger);
});

// Build and run the application
var app = builder.Build();
await app.RunAsync();
