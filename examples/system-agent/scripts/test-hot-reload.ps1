# Hot-Reload Configuration Update Test Script
# Tests configuration validation and actual behavior changes when receiving updates via NATS

$ErrorActionPreference = "Continue"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Hot-Reload Configuration Update Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[PREREQUISITES]" -ForegroundColor Yellow
Write-Host "   1. NATS server must be running and accessible" -ForegroundColor Gray
Write-Host "   2. Device must be registered and subscribed to config update topic" -ForegroundColor Gray
Write-Host "   3. appsettings.json must have correct DeviceName and NATS URL" -ForegroundColor Gray
Write-Host ""

Write-Host "[TEST SCENARIOS]" -ForegroundColor Yellow
Write-Host "   1. Valid config update → Should apply successfully" -ForegroundColor Gray
Write-Host "   2. Invalid NATS URL → Should reject with error message" -ForegroundColor Gray
Write-Host "   3. Changed sensor interval → Should restart background tasks" -ForegroundColor Gray
Write-Host ""

Write-Host "[CONFIGURATION UPDATE FLOW]" -ForegroundColor Yellow
Write-Host "   Cloud → NATS pub → WedaCloudService.SubscribeConfigurationUpdatesAsync()" -ForegroundColor Gray
Write-Host "   → DeviceBase.ApplyBaseConfigurationUpdateAsync()" -ForegroundColor Gray
Write-Host "   → OnBeforeConfigUpdateAsync() [VALIDATION HERE]" -ForegroundColor Gray
Write-Host "   → ValidateConfigurationUpdate(message)" -ForegroundColor Gray
Write-Host "   → ConfigurationUpdateHelper.ValidateDeviceConfiguration()" -ForegroundColor Gray
Write-Host "   → StartupConfigurationValidator.ValidateDeviceConfig()" -ForegroundColor Gray
Write-Host ""

Write-Host "[VALIDATION CHECKS]" -ForegroundColor Yellow
Write-Host "   [OK] DeviceName must not be empty" -ForegroundColor Gray
Write-Host "   [OK] NATS URL must be in format nats://host:port or tls://host:port" -ForegroundColor Gray
Write-Host "   [OK] Sensor intervals must be positive (0 = warning, use default 10000ms)" -ForegroundColor Gray
Write-Host "   [OK] ReportHealth period must be non-negative" -ForegroundColor Gray
Write-Host ""

Write-Host "[HOW TO TRIGGER CONFIG UPDATE]" -ForegroundColor Yellow
Write-Host "   Option A - Use nats CLI (recommended):" -ForegroundColor Green
Write-Host '   nats pub "edgesync.device.<DeviceId>.config.update" @update-message.json' -ForegroundColor Gray
Write-Host ""
Write-Host "   Option B - Use PowerShell with NATS.Net:" -ForegroundColor Green
Write-Host "   # See example script below" -ForegroundColor Gray
Write-Host ""

Write-Host "[EXAMPLE UPDATE MESSAGE (update-message.json)]" -ForegroundColor Yellow
$exampleMessage = @'
{
  "Cmd": "config.update",
  "DeviceId": "System-Collector-Advantech-ARM64",
  "SeqId": 1,
  "Timestamp": 1734566400000,
  "Data": {
    "DeviceId": "System-Collector-Advantech-ARM64",
    "ConfigVersion": "1.0.1",
    "UpdateType": "full",
    "Cfg": {
      "Desired": {
        "SubNodeDeviceConfig": {
          "DeviceConfigs": {
            "SystemMonitorDeviceConfig": {
              "DeviceName": "System-Collector-Advantech-ARM64",
              "Periods": {
                "ReportHealth": 30000
              },
              "Sensors": [
                {
                  "Name": "cpu.usage",
                  "Config": {
                    "Enabled": true,
                    "Interval": 2000
                  }
                }
              ]
            }
          }
        }
      }
    }
  }
}
'@
Write-Host $exampleMessage -ForegroundColor Gray
Write-Host ""

Write-Host "[INVALID CONFIG EXAMPLE (should fail validation)]" -ForegroundColor Red
$invalidExample = @'
{
  "Cmd": "config.update",
  ...
  "Cfg": {
    "Desired": {
      "SubNodeDeviceConfig": {
        "DeviceConfigs": {
          "SystemMonitorDeviceConfig": {
            "DeviceName": "",  <- Empty DeviceName (INVALID)
            "Sensors": [
              {
                "Name": "cpu.usage",
                "Config": {
                  "Interval": -1  <- Negative interval (INVALID)
                }
              }
            ]
          }
        }
      }
    }
  }
}
'@
Write-Host $invalidExample -ForegroundColor Gray
Write-Host ""

Write-Host "[EXPECTED BEHAVIOR ON VALIDATION FAILURE]" -ForegroundColor Yellow
Write-Host "   1. OnBeforeConfigUpdateAsync throws InvalidOperationException" -ForegroundColor Gray
Write-Host "   2. ApplyBaseConfigurationUpdateAsync catches exception" -ForegroundColor Gray
Write-Host "   3. Configuration is NOT applied (rollback)" -ForegroundColor Gray
Write-Host "   4. 'invalid' status report sent to cloud with error message" -ForegroundColor Gray
Write-Host "   5. Log shows: '[HOT-RELOAD] Configuration update validation failed: <error>'" -ForegroundColor Gray
Write-Host "   6. Log shows: 'Exit Code: 4 (InvalidConfigValue)'" -ForegroundColor Gray
Write-Host ""

Write-Host "[EXPECTED BEHAVIOR ON SUCCESSFUL UPDATE]" -ForegroundColor Yellow
Write-Host "   1. [HOT-RELOAD] Configuration update received" -ForegroundColor Gray
Write-Host "   2. Validation passes" -ForegroundColor Gray
Write-Host "   3. Configuration backup created" -ForegroundColor Gray
Write-Host "   4. Sensor intervals/periods updated in memory" -ForegroundColor Gray
Write-Host "   5. Configuration cached to .weda/<DeviceName>.config.json" -ForegroundColor Gray
Write-Host "   6. If intervals changed: Background tasks restarted" -ForegroundColor Gray
Write-Host "   7. 'success' status report sent to cloud" -ForegroundColor Gray
Write-Host "   8. VERIFY: Observe telemetry frequency changes in logs" -ForegroundColor Gray
Write-Host ""

Write-Host "[VERIFICATION STEPS FOR INTERVAL CHANGES]" -ForegroundColor Cyan
Write-Host "   Before Update:" -ForegroundColor Yellow
Write-Host "   1. Start system-monitor application" -ForegroundColor Gray
Write-Host "   2. Observe current telemetry frequency in logs" -ForegroundColor Gray
Write-Host "      Example: '[10:30:45.123 INF] cpu.usage: 25.5%' (every 500ms)" -ForegroundColor Gray
Write-Host ""
Write-Host "   Send Update:" -ForegroundColor Yellow
Write-Host "   3. Change cpu.usage Interval from 500ms to 2000ms" -ForegroundColor Gray
Write-Host "   4. Publish config update via NATS" -ForegroundColor Gray
Write-Host ""
Write-Host "   After Update:" -ForegroundColor Yellow
Write-Host "   5. Watch logs for '[HOT-RELOAD] Configuration update received'" -ForegroundColor Gray
Write-Host "   6. Verify 'Background task config changed, restarting tasks'" -ForegroundColor Gray
Write-Host "   7. Observe NEW telemetry frequency" -ForegroundColor Gray
Write-Host "      Expected: '[10:31:00.456 INF] cpu.usage: 26.3%' (now every 2000ms)" -ForegroundColor Gray
Write-Host "   8. Confirm cached config updated: .weda/System-Collector-Advantech-ARM64.config.json" -ForegroundColor Gray
Write-Host ""

Write-Host "[SAMPLE POWERSHELL SCRIPT TO PUBLISH UPDATE]" -ForegroundColor Cyan
$publishScript = @'
# Install NATS.Net if not already installed
# dotnet add package NATS.Net

# Sample publish script (pseudo-code)
$natsUrl = "nats://172.22.160.197:4224"
$topic = "edgesync.device.System-Collector-Advantech-ARM64.config.update"
$message = Get-Content update-message.json -Raw

# Using nats CLI (recommended):
nats pub $topic $message

# Or using .NET:
# var client = new NatsClient(new NatsOpts { Url = natsUrl });
# await client.PublishAsync<string>(topic, message);
'@
Write-Host $publishScript -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Manual Testing Required" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`n[CHECKLIST]" -ForegroundColor Yellow
Write-Host "   [ ] 1. Start the system-monitor application" -ForegroundColor Green
Write-Host "   [ ] 2. Note current telemetry frequency (e.g., cpu.usage logs)" -ForegroundColor Green
Write-Host "   [ ] 3. Create update-message.json with changed interval" -ForegroundColor Green
Write-Host "   [ ] 4. Publish message using NATS CLI or client" -ForegroundColor Green
Write-Host "   [ ] 5. Watch for [HOT-RELOAD] log messages" -ForegroundColor Green
Write-Host "   [ ] 6. Verify validation errors (if testing invalid config)" -ForegroundColor Green
Write-Host "   [ ] 7. Verify telemetry frequency changed (if testing valid config)" -ForegroundColor Green
Write-Host "   [ ] 8. Check cached config file was updated" -ForegroundColor Green
Write-Host ""
Write-Host "[RELATED DOCUMENTATION]" -ForegroundColor Cyan
Write-Host "   - docs/EXIT_CODES.md (exit code reference)" -ForegroundColor Gray
Write-Host "   - docs/CONFIGURATION_VALIDATION.md (validation rules)" -ForegroundColor Gray
Write-Host "   - scripts/test-config-validation.ps1 (startup validation tests)" -ForegroundColor Gray
Write-Host ""
