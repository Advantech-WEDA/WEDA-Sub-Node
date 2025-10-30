using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Host.Context;
using Wise4012Example;

// ═══════════════════════════════════════════════════════════════════════════
// SubNode Template - MyFirstDevice Pattern
// ═══════════════════════════════════════════════════════════════════════════
// Usage:
//   dotnet run          - Start the device
//   dotnet run -- --scan - Scan and generate sensor suggestions
// ═══════════════════════════════════════════════════════════════════════════

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger));

try
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Code start from here
    // ═══════════════════════════════════════════════════════════════════════════{
    using var context = new WedaApplicationContext(configuration, loggerFactory);
    var device = new MyFirstDevice(context);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }
    
    // if (args.Contains("--scan"))
    // {
    //     await ScanAndGenerateReport(device);
    //     device.Dispose();
    //     return;
    // }

    // Normal mode: run device
    await device.StartAsync();
    Log.Information("Device started. Press Ctrl+C to stop...");


    // ═══════════════════════════════════════════════════════════════════════════
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);

    await device.StopAsync();
    device.Dispose();
}
catch (OperationCanceledException)
{
    Log.Information("Application cancelled");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task ScanAndGenerateReport(MyFirstDevice device)
{
    var scanConfig = new ModbusScanConfig
    {
        StartAddress = 0,
        EndAddress = 99,
        RegistersPerScan = 4,
        DelayBetweenScans = 100
    };

    Log.Information("Scanning addresses 0-99...");
    var results = await device.ScanRegistersAsync(scanConfig);

    var reportPath = $"modbus_scan_report_{DateTime.Now:yyyyMMdd_HHmmss}.md";
    await File.WriteAllTextAsync(reportPath, device.GenerateScanReport(results, scanConfig));
    Log.Information("Report saved: {ReportPath}", reportPath);

    var suggestions = device.GenerateSensorSuggestions(results);
    if (suggestions.Count > 0)
    {
        Log.Information("\nSuggested sensor configuration:\n\"Sensors\": [");
        for (int i = 0; i < suggestions.Count; i++)
        {
            var s = suggestions[i];
            var comma = i < suggestions.Count - 1 ? "," : "";
            Log.Information("  {{\n    \"Name\": \"{Name}\",\n    \"Dtmi\": \"dtmi:advantech:EdgeSync:Sensor;1\",\n    \"Parameters\": {{\n      \"RegisterType\": \"HoldingRegister\",\n      \"RegisterAddress\": {Addr},\n      \"RegisterCount\": {Count},\n      \"DataType\": \"{Type}\"\n    }},\n    \"Config\": {{ \"Enabled\": true, \"Interval\": 1000 }}\n  }}{Comma}",
                s.SuggestedName, s.RegisterAddress, s.RegisterCount, s.SuggestedDataType, comma);
        }
        Log.Information("]");
    }
    else
    {
        Log.Warning("No sensors found. Check device is powered on and settings are correct.");
    }
}
