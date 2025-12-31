using Wise4012PlusExample;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    WISE-4012 Modbus Device (Plus)");
Console.WriteLine("===========================================");
Console.WriteLine();

// Create application with default builder (includes real cloud service)
var builder = WedaApplication.CreateDefaultBuilder(args);

// Register MyModbusDevice - reads config from devicecfg.json DeviceConfigs["MyModbusDevice"]
builder.AddDevice<MyModbusDevice>("MyModbusDevice");

// Build and run the application
var app = builder.Build();

Console.WriteLine("Starting WISE-4012 Modbus device...");
Console.WriteLine("Reading 4 AI channels every 3 seconds.");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();
