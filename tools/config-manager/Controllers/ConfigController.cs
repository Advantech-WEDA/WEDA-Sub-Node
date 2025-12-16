using System.Text.Json;
using System.Text.Json.Serialization;
using ConfigManager.Models;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;
using NATS.Net;

namespace ConfigManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// GET /api/config?path={projectPath}
    /// Reads appsettings.json and .weda files from the specified project path
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetConfig([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        if (!Directory.Exists(path))
        {
            return BadRequest(new { error = $"Directory not found: {path}" });
        }

        var result = new Dictionary<string, object?>();

        // Read appsettings.json
        var appSettingsPath = Path.Combine(path, "appsettings.json");
        if (System.IO.File.Exists(appSettingsPath))
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                result["appSettings"] = JsonSerializer.Deserialize<JsonElement>(content);
                result["appSettingsPath"] = appSettingsPath;
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = $"Invalid JSON in appsettings.json: {ex.Message}" });
            }
        }
        else
        {
            return BadRequest(new { error = $"appsettings.json not found in {path}" });
        }

        // Read .weda/subnode.registration.json
        var registrationPath = Path.Combine(path, ".weda", "subnode.registration.json");
        if (System.IO.File.Exists(registrationPath))
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(registrationPath);
                result["registration"] = JsonSerializer.Deserialize<RegistrationData>(content, JsonOptions);
                result["registrationPath"] = registrationPath;
            }
            catch (JsonException ex)
            {
                // Registration file is optional, just log and continue
                result["registrationError"] = ex.Message;
            }
        }

        // Read .weda/config.cache.json
        var configCachePath = Path.Combine(path, ".weda", "config.cache.json");
        if (System.IO.File.Exists(configCachePath))
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(configCachePath);
                result["configCache"] = JsonSerializer.Deserialize<JsonElement>(content);
                result["configCachePath"] = configCachePath;
            }
            catch (JsonException ex)
            {
                result["configCacheError"] = ex.Message;
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// GET /api/config/browse?path={directoryPath}
    /// Lists directories and checks for appsettings.json
    /// </summary>
    [HttpGet("browse")]
    public ActionResult BrowseDirectory([FromQuery] string? path)
    {
        try
        {
            // Default to user's home directory
            if (string.IsNullOrEmpty(path))
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(path))
                {
                    path = OperatingSystem.IsWindows() ? "C:\\" : "/";
                }
            }

            if (!Directory.Exists(path))
            {
                return BadRequest(new { error = $"Directory not found: {path}" });
            }

            var dirInfo = new DirectoryInfo(path);
            var directories = new List<object>();

            // Add parent directory option if not at root
            if (dirInfo.Parent != null)
            {
                directories.Add(new
                {
                    name = "..",
                    path = dirInfo.Parent.FullName,
                    isParent = true
                });
            }

            // List subdirectories
            try
            {
                foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                {
                    // Skip hidden directories (except .weda)
                    if (dir.Name.StartsWith(".") && dir.Name != ".weda") continue;

                    try
                    {
                        directories.Add(new
                        {
                            name = dir.Name,
                            path = dir.FullName,
                            isParent = false
                        });
                    }
                    catch
                    {
                        // Skip directories we can't access
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return BadRequest(new { error = "Access denied to this directory" });
            }

            // Check if current directory has appsettings.json
            var hasAppSettings = System.IO.File.Exists(Path.Combine(path, "appsettings.json"));

            return Ok(new
            {
                currentPath = path,
                directories,
                hasAppSettings
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/config/apply
    /// Sends configuration update to device via NATS
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult<ApplyConfigResponse>> ApplyConfig([FromBody] ApplyConfigRequest request)
    {
        if (request.Registration == null || string.IsNullOrEmpty(request.Registration.DeviceId))
        {
            return BadRequest(new ApplyConfigResponse
            {
                Success = false,
                Error = "Missing registration data"
            });
        }

        if (request.DeviceConfigs == null)
        {
            return BadRequest(new ApplyConfigResponse
            {
                Success = false,
                Error = "Missing device configuration"
            });
        }

        try
        {
            // Build NATS connection options
            var natsOpts = new NatsOpts
            {
                Url = request.NatsUrl ?? "nats://localhost:4222",
                Name = "config-manager",
                SerializerRegistry = NatsJsonSerializerRegistry.Default
            };

            // Add auth if provided
            if (!string.IsNullOrEmpty(request.NatsUsername) && !string.IsNullOrEmpty(request.NatsPassword))
            {
                natsOpts = natsOpts with
                {
                    AuthOpts = new NatsAuthOpts
                    {
                        Username = request.NatsUsername,
                        Password = request.NatsPassword
                    }
                };
            }

            await using var nats = new NatsClient(natsOpts);

            var reqSeqId = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Build the configuration update message
            var message = new
            {
                protoVer = "eco1j",
                groupId = "weda",
                deviceId = request.Registration.DeviceId,
                cmd = "updateCmd",
                seqId = timestamp,
                reqSeqId,
                rspSeqId = Guid.NewGuid().ToString(),
                timestamp,
                data = new
                {
                    cfg = new
                    {
                        desired = new
                        {
                            subNodeDeviceConfig = new
                            {
                                DeviceConfigs = request.DeviceConfigs
                            }
                        }
                    }
                }
            };

            // Get the config update topic from registration
            var topic = request.Registration.NatsTopicAssignments?.ConfigUpdateTopic;
            if (string.IsNullOrEmpty(topic))
            {
                return BadRequest(new ApplyConfigResponse
                {
                    Success = false,
                    Error = "ConfigUpdateTopic not found in registration"
                });
            }

            // Publish to NATS
            await nats.PublishAsync(topic, message);

            return Ok(new ApplyConfigResponse
            {
                Success = true,
                Topic = topic,
                MessageId = reqSeqId,
                Timestamp = timestamp,
                Payload = message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApplyConfigResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}
