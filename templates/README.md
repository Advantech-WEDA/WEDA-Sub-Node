# weda SubNode Templates

This package provides .NET templates for creating weda SubNode applications.

## Installation

### Step 1: Pack and Install Template

From the repository root:

```bash
# Navigate to templates directory
cd templates

# Pack the template
dotnet pack

# Install the template
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg
```

Or install from a published NuGet package:

```bash
dotnet new install Weda.SubNode.Templates
```

### Step 2: Verify Installation

```bash
dotnet new list | grep subnode
```

You should see:
```
weda SubNode Console Application  subnode  [C#]  Console/IoT/weda/SubNode
```

## Usage

### Create a New SubNode Application

⚠️ **Important**: The template must be used within the repository, typically in the `examples/` directory, as it references the local `Weda.SubNode.Core` project.

#### Basic Usage

```bash
# Navigate to examples directory
cd examples

# Create new SubNode application (automatically runs dotnet restore)
dotnet new subnode -n MySubNodeApp

# Navigate to the created project
cd MySubNodeApp

# Build and run
dotnet build
dotnet run
```

> **Note**: The template automatically runs `dotnet restore` after project creation to ensure all references are resolved. This prevents IDE errors before the first build.

#### What Gets Created

The template generates:

```
MySubNodeApp/
├── Program.cs              # Main application entry point
├── appsettings.json        # Configuration file
└── MySubNodeApp.csproj   # Project file with references
```

**Program.cs** includes:
- Configuration and Serilog setup
- Device initialization workflow
- Modbus TCP communication
- Cloud service integration (NullCloudService)
- Telemetry reading and reporting
- Background task management

**appsettings.json** contains:
- Serilog logging configuration
- SubNode settings (DeviceName, DeviceType, etc.)
- Communication settings (Host, Port, SlaveId)
- Sensor definitions with DTDL support

**Project file** includes:
- ProjectReference to `../../src/Weda.SubNode.Core`
- All required Serilog packages
- Microsoft.Extensions.Configuration packages

### Customization

After creating your project, customize these key areas:

1. **Device Configuration** (`appsettings.json`):
   ```json
   {
     "SubNode": {
       "DeviceName": "MyCustomDevice",
       "DeviceType": "modbusEthernet",
       "Communication": {
         "Host": "192.168.1.100",
         "Port": 502,
         "SlaveId": 1
       },
       "Sensors": [
         // Add your sensor definitions
       ]
     }
   }
   ```

2. **Communication Protocol**:
   - Template uses `TcpCommunication` for Modbus TCP
   - Modify `Program.cs` if you need different protocols

3. **Cloud Service**:
   - Template uses `NullCloudService` for testing
   - Replace with your actual cloud service implementation

## Available Templates

### `subnode` - SubNode Console Application

A console application template demonstrating the SubNode SDK usage pattern:

**Features:**
- ✅ Real device connection via Modbus TCP
- ✅ Cloud service integration (NullCloudService for testing)
- ✅ Telemetry reading and reporting
- ✅ UUID5-based resource ID generation
- ✅ Serilog structured logging
- ✅ Configuration-driven device setup
- ✅ DTDL (Digital Twins Definition Language) support

**Prerequisites:**
- .NET 9.0 SDK
- Modbus device or simulator running (default: 127.0.0.1:502)
- For examples with ModbusSimulator: Start `ModbusSimulatorExample` first

## Examples

### Example 1: Basic Modbus Device

```bash
cd examples
dotnet new subnode -n MyModbusDevice
cd MyModbusDevice

# Edit appsettings.json to configure your device
# Then build and run
dotnet run
```

### Example 2: Multiple Devices

```bash
cd examples
dotnet new subnode -n Device1
dotnet new subnode -n Device2

# Configure each device separately
# Each has independent configuration
```

## Troubleshooting

### Issue: Build fails with "cannot find Weda.SubNode.Core"

**Solution**: Ensure you're creating the project within the repository structure, particularly under `examples/` directory. The template uses `../../src/Weda.SubNode.Core` as the project reference path.

### Issue: Cannot connect to Modbus device

**Solution**:
1. Verify the device/simulator is running
2. Check Host and Port in `appsettings.json`
3. Ensure firewall allows the connection

### Issue: NuGet warnings about missing version bounds

**Solution**: These are warnings from packages without version specifications. The build will succeed. If needed, you can specify versions explicitly in the `.csproj` file.

## Uninstall

```bash
dotnet new uninstall Weda.SubNode.Templates
```

## Template Development

After making changes to the templates:

### Quick Update (Recommended)

```bash
cd templates
chmod +x update-template.sh
./update-template.sh
```

### Manual Update

```bash
# Rebuild the package
cd templates
dotnet pack -c Release

# Uninstall old version
dotnet new uninstall Weda.SubNode.Templates

# Install new version
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg

# Test in examples directory
cd ../examples
dotnet new subnode -n TestApp
cd TestApp
dotnet build
```

### Important Notes

⚠️ **Template updates require reinstallation**: When you modify files in `templates/subnode/`, you must:
1. Rebuild the package with `dotnet pack`
2. Uninstall the old template
3. Install the new package

Simply modifying the source files **will NOT** affect `dotnet new` - it uses the installed package, not the source files.

## Integration with Repository

This template is designed to integrate with the `weda_subnode` repository structure:

```
weda_subnode/
├── src/
│   └── Weda.SubNode.Core/        # Core SDK (referenced by template)
├── examples/
│   ├── ModbusDeviceExample/
│   └── [Your new projects here]    # ← Create new projects here
└── templates/
    └── subnode/                   # Template source
```

## Support

For issues or questions:
1. Check the ModbusDeviceExample in `examples/` directory
2. Review appsettings.json configuration
3. Check Program.cs for initialization flow
