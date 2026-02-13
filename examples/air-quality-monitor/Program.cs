using Weda.SubNode.Host;
using Weda.SubNode.WebApi;
using AirQualityMonitor.Devices;

Console.WriteLine("===========================================");
Console.WriteLine("    Air Quality Monitor - Taiwan MOENV");
Console.WriteLine("    Dynamic Storage Test (application/json)");
Console.WriteLine("===========================================");
Console.WriteLine();

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud()
    .AddWebApi();

builder.AddDevice<AirQualityDevice>("AirQualityConfig");

var app = builder.Build();

Console.WriteLine("Starting air quality monitor...");
Console.WriteLine("Data will be fetched every hour.");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();
