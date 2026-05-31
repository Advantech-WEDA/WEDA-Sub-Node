using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Cloud.Clients.DeviceManagement.Mapping;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Telemetry;

// capdump <example-dir> [output-file]
//
// Loads the example's devicecfg.json (+ optional systemcfg.json / customcfg.json),
// builds a DeviceConfigurationDto using the SDK's real ToConfigurationDto pipeline,
// and prints it as JSON. Output is what the SubNode would upload to the cloud at
// registration / cfg-update time.
//
// Device classes are NOT instantiated; only configuration binding + DTDL emission
// run. This is a pure dump utility — no NATS, no devices, no hardware.

if (args.Length < 1 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Usage: capdump <example-dir> [output-file]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  example-dir   path to an example folder containing devicecfg.json");
    Console.Error.WriteLine("  output-file   write JSON here (default: stdout)");
    return args.Length < 1 ? 1 : 0;
}

var exampleDir = Path.GetFullPath(args[0]);
var outputFile = args.Length > 1 ? args[1] : null;

var devCfgPath = Path.Combine(exampleDir, "devicecfg.json");
if (!File.Exists(devCfgPath))
{
    Console.Error.WriteLine($"devicecfg.json not found at {devCfgPath}");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(exampleDir)
    .AddJsonFile("devicecfg.json", optional: false)
    .AddJsonFile("systemcfg.json", optional: true)
    .AddJsonFile("customcfg.json", optional: true)
    .Build();

var subNodeInfo = configuration.GetSection("SubNode").Get<SubNodeInfo>()
    ?? new SubNodeInfo { Name = "<unknown>" };
subNodeInfo.DeviceId ??= "00000000-0000-0000-0000-000000000000";

// Probe the example's compiled assembly for an IDevice + [DeviceType] pair
// (the same signal the host uses to mark a section as strongly-typed). When
// found, we apply its DeviceTypeName to every DeviceConfigs section in this
// example. Limitation: assumes 1 device class per example — fine for current
// 10 examples; multi-device dumps need explicit section→class mapping.
// Also: register the assembly with both registries so consumer-defined
// IConfigurableDevice / IConfigurableSensor land in the catalog.
string? exampleDeviceTypeName = null;
var probedAssembly = ProbeExampleAssembly(exampleDir);
if (probedAssembly is not null)
{
    SensorTypeRegistry.RegisterAssemblies(probedAssembly);
    DeviceTypeRegistry.RegisterAssemblies(probedAssembly);

    var deviceClass = probedAssembly.GetTypes().FirstOrDefault(t =>
        !t.IsAbstract && !t.IsInterface &&
        typeof(IDevice).IsAssignableFrom(t) &&
        t.GetCustomAttribute<DeviceTypeAttribute>(inherit: true) is not null);

    exampleDeviceTypeName = deviceClass?
        .GetCustomAttribute<DeviceTypeAttribute>(inherit: true)?
        .DeviceTypeName;
}
else
{
    // Still need to touch the registries so the SDK auto-scan happens and the
    // TypedSensorDispatch.Resolve hook is installed before InitializeDtdl.
    _ = SensorTypeRegistry.HasAny;
    _ = DeviceTypeRegistry.All;
}

var configs = new DeviceConfigurations();
foreach (var section in configuration.GetSection("DeviceConfigs").GetChildren())
{
    DeviceConfiguration? dc;
    try { dc = section.Get<DeviceConfiguration>(); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  [warn] failed to bind '{section.Key}': {ex.Message}");
        continue;
    }
    if (dc is null) continue;

    if (string.IsNullOrWhiteSpace(dc.DeviceName)) dc.DeviceName = section.Key;
    dc.SubNodeInfo = subNodeInfo;
    dc.DeviceId = subNodeInfo.DeviceId;
    dc.DeviceTypeName = exampleDeviceTypeName;  // null → untyped fallback in InitializeDtdl

    // Recover array Parameters mangled by IConfiguration → Dictionary bind so
    // typed dispatch's try-validate sees proper string[] for Interfaces/PinIds/Sources.
    SensorParameterNormalizer.NormalizeArrayParameters(dc, section);


    var deviceResourceId = $"dump-{section.Key.ToLowerInvariant()}";
    foreach (var sensor in dc.Sensors)
    {
        if (string.IsNullOrEmpty(sensor.ResourceId)) sensor.ResourceId = Guid.NewGuid().ToString();
        sensor.DeviceResourceId = deviceResourceId;
    }

    // InitializeDtdl auto-finds solution root for relative DtdlPath; passing
    // exampleDir would double the path (DtdlPath in devicecfg is repo-relative).
    try { dc.InitializeDtdl(); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  [warn] InitializeDtdl failed for '{section.Key}': {ex.Message.Split('\n')[0]}");
    }

    configs[section.Key] = dc;
}

if (configs.Count == 0)
{
    Console.Error.WriteLine("No DeviceConfigs sections found / bound.");
    return 1;
}

var registry = new CommandRegistry();
registry.ScanAssembly(typeof(CommandRegistry).Assembly);
if (probedAssembly is not null) registry.ScanAssembly(probedAssembly);

DeviceConfigurationDto dto;
try { dto = configs.ToConfigurationDto(registry); }
catch (Exception ex)
{
    Console.Error.WriteLine($"ToConfigurationDto failed: {ex.Message}");
    return 2;
}

var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });

static Assembly? ProbeExampleAssembly(string exampleDir)
{
    // Walk bin/{Debug|Release}/net*/ for a dll that's NOT one of the SDK
    // assemblies and looks like the example's own (matches the dir name
    // case-insensitively, ignoring punctuation).
    var binDir = Path.Combine(exampleDir, "bin");
    if (!Directory.Exists(binDir)) return null;

    var exampleSlug = new string(Path.GetFileName(exampleDir.TrimEnd(Path.DirectorySeparatorChar))
        .Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // capdump targets net10.0 — only consider matching framework subdirs to
    // avoid loading a net9.0 dll into a net10.0 process (mismatch can silently
    // pick up a stale build of a referenced assembly such as Weda.SubNode.Devices).
    var candidates = Directory.EnumerateFiles(binDir, "*.dll", SearchOption.AllDirectories)
        .Where(p => p.Contains("net10.0", StringComparison.Ordinal))
        .OrderByDescending(File.GetLastWriteTimeUtc);
    foreach (var path in candidates)
    {
        var stem = new string(Path.GetFileNameWithoutExtension(path)
            .Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (!stem.Contains(exampleSlug, StringComparison.OrdinalIgnoreCase) &&
            !exampleSlug.Contains(stem, StringComparison.OrdinalIgnoreCase)) continue;
        try { return Assembly.LoadFrom(path); }
        catch { /* try next */ }
    }
    return null;
}

if (outputFile is not null)
{
    var fullOut = Path.GetFullPath(outputFile);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
    File.WriteAllText(fullOut, json);
    Console.Error.WriteLine($"Wrote {fullOut} ({json.Length} bytes)");
}
else
{
    Console.WriteLine(json);
}
return 0;
