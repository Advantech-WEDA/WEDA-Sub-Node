using Weda.SubNode.Host;

// ═══════════════════════════════════════════════════════════════════════════
// WedaApplication - CreateDefaultBuilder Pattern (Recommended)
// ═══════════════════════════════════════════════════════════════════════════
// This approach provides:
// - Automatic Serilog configuration from appsettings.json
// - Automatic device scanning from "Devices" section
// - Built-in cloud service integration
// - Telemetry and health reporting
// ═══════════════════════════════════════════════════════════════════════════

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
