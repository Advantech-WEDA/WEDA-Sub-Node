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

// Probe the example's compiled assembly for IDevice + [DeviceType] pairs and
// register the assembly with the SDK capability registries so consumer-defined
// IConfigurableDevice / IConfigurableSensor land in the catalog. Multi-device
// examples (e.g. power-aggregation: tcp-modbus + aggregator) collect every
// observed DeviceTypeName; per-section we then try each candidate in order
// and pick the first one whose typed dispatch resolves cleanly.
var candidateDeviceTypeNames = new List<string>();
var probedAssembly = ProbeExampleAssembly(exampleDir);
if (probedAssembly is not null)
{
    SensorTypeRegistry.RegisterAssemblies(probedAssembly);
    DeviceTypeRegistry.RegisterAssemblies(probedAssembly);

    foreach (var type in probedAssembly.GetTypes())
    {
        if (type.IsAbstract || type.IsInterface) continue;
        var isDevice = typeof(IDevice).IsAssignableFrom(type);
        // Inherited attribute lookup walks the runtime base chain; if Type.GetCustomAttribute's
        // default-context DeviceTypeAttribute differs from the LoadFrom'd one (ALC trap), match
        // by attribute name string instead so the probe stays robust.
        var attrByName = type.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == typeof(DeviceTypeAttribute).FullName);
        string? dtnFromAttr = attrByName?.ConstructorArguments.FirstOrDefault().Value as string;
        if (isDevice && dtnFromAttr is not null)
        {
            if (!candidateDeviceTypeNames.Contains(dtnFromAttr))
                candidateDeviceTypeNames.Add(dtnFromAttr);
        }
        // For derived types, walk base hierarchy manually for inherited attribute (matches Attribute.GetCustomAttribute(inherit:true)).
        if (isDevice && dtnFromAttr is null)
        {
            for (var bt = type.BaseType; bt is not null && bt != typeof(object); bt = bt.BaseType)
            {
                var baseAttr = bt.GetCustomAttributesData()
                    .FirstOrDefault(a => a.AttributeType.FullName == typeof(DeviceTypeAttribute).FullName);
                if (baseAttr?.ConstructorArguments.FirstOrDefault().Value is string s)
                {
                    if (!candidateDeviceTypeNames.Contains(s))
                        candidateDeviceTypeNames.Add(s);
                    break;
                }
            }
        }
    }
    Console.Error.WriteLine($"probed {Path.GetFileName(probedAssembly.Location)}: " +
        $"device types [{string.Join(", ", candidateDeviceTypeNames)}]");
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

    // Recover array Parameters mangled by IConfiguration → Dictionary bind so
    // typed dispatch's try-validate sees proper string[] for Interfaces/PinIds/Sources.
    SensorParameterNormalizer.NormalizeArrayParameters(dc, section);

    // Per-section device-type resolution: in the host, the loader knows
    // (sectionName → AddDevice<TDevice>'s type) directly. capdump has no
    // such map — we try each candidate DeviceTypeName from the probed
    // assembly and keep the one whose typed dispatch successfully resolves
    // every sensor in this section.
    dc.DeviceTypeName = ResolveDeviceTypeForSection(dc, candidateDeviceTypeNames);


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

static string? ResolveDeviceTypeForSection(
    DeviceConfiguration dc, IReadOnlyList<string> candidates)
{
    if (candidates.Count == 0) return null;
    if (candidates.Count == 1) return candidates[0];

    // Multiple device types found in this example (e.g. power-aggregation has
    // tcp-modbus + aggregator). Try each against THIS section's sensors;
    // pick the one whose typed dispatch resolves every sensor — that's the
    // section's intended device type.
    foreach (var candidate in candidates)
    {
        var ok = dc.Sensors.All(s =>
        {
            try { _ = SensorTypeRegistry.Resolve(candidate, s); return true; }
            catch { return false; }
        });
        if (ok) return candidate;
    }
    // No clean match — let the loader fall back to untyped autogen.
    return null;
}

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
