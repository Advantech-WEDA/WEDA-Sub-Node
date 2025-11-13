English / [繁體中文](README_zh.md)

# Dev Container for Weda SubNode SDK

This development container provides a fully configured environment for developing with the Weda SubNode SDK, including:

- **.NET 9.0 SDK** - Latest .NET development tools
- **Weda SubNode Templates** - Pre-installed project templates
- **VS Code Extensions** - Pre-installed C#, Docker, Git extensions
- **CLI Tools** - Entity Framework CLI, dotnet-format, etc.

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed
- [Visual Studio Code](https://code.visualstudio.com/) installed
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) installed

### Using the Dev Container

1. **Open the project in VS Code**
   ```bash
   code /path/to/edge_subnode
   ```

2. **Reopen in Container**
   - Press `F1` or `Cmd/Ctrl+Shift+P`
   - Select: `Dev Containers: Reopen in Container`
   - Wait for the container to build and start (first time may take a few minutes)

3. **Start Developing**
   - The container will automatically install templates, run `dotnet restore` and `dotnet build`
   - You can immediately start coding!

## What's Included

### Installed Tools

- **dotnet-format** - Code formatter
- **dotnet-outdated-tool** - Check for outdated packages
- **Git** - Version control
- **GitHub CLI** - GitHub command-line tool
- **Weda SubNode Templates** - Pre-installed project templates (wedaapi, subnode)

> **Note**: `dotnet-ef` is not pre-installed due to package issues, but can be installed manually if needed: `dotnet tool install --global dotnet-ef`

### VS Code Extensions

- **C# Dev Kit** - Full C# development experience
- **GitLens** - Git supercharged
- **Docker** - Docker container management
- **EditorConfig** - Consistent coding styles
- **Markdown All in One** - Markdown support

## Common Tasks

### Build the Solution

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Create a New Device Project

Templates are pre-installed in the dev container, so you can create projects immediately:

```bash
# Create a new Web API style project (recommended for production)
mkdir devices
cd devices
dotnet new wedaapi -n MyNewDevice
cd ..
dotnet sln add devices/MyNewDevice/MyNewDevice.csproj

# Or create a console style project (for development/debugging)
mkdir devices
cd devices
dotnet new subnode -n MyDebugDevice
cd ..
dotnet sln add devices/MyDebugDevice/MyDebugDevice.csproj

# List available templates
dotnet new list | grep -i weda
```

## Troubleshooting

### Container won't start

1. Make sure Docker Desktop is running
2. Try rebuilding the container:
   - `F1` → `Dev Containers: Rebuild Container`
3. Check Docker logs for errors

### Template installation fails

If templates fail to install, you can manually install them:
```bash
dotnet new install ./templates
```

### Build errors

If you encounter build errors after container starts:
```bash
dotnet clean
dotnet restore
dotnet build
```

## Customization

### Add more VS Code extensions

Edit `.devcontainer/devcontainer.json` and add extensions to the `extensions` array:

```json
"extensions": [
  "ms-dotnettools.csharp",
  "your-extension-id-here"
]
```

### Modify Dockerfile

You can customize the Dockerfile to add more tools or change configurations:

```dockerfile
# Add your custom tools
RUN apt-get update && apt-get install -y your-package
```

## Performance Tips

### Use WSL2 on Windows

For better performance on Windows, use WSL2 as the Docker backend.

### Exclude bin/obj folders

The dev container is configured to exclude `bin/` and `obj/` folders from the VS Code file watcher for better performance.

### Use volume mounts

The workspace is mounted with the `:cached` flag for better performance on macOS.

## Additional Resources

- [VS Code Dev Containers Documentation](https://code.visualstudio.com/docs/devcontainers/containers)
- [NATS Documentation](https://docs.nats.io/)
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Weda SubNode SDK Documentation](../docs/wiki/zh/README.md)
