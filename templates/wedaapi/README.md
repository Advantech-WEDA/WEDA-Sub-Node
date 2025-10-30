# WedaApi

weda SubNode API application.

## Run

```bash
dotnet run
```

## Configuration

Edit `appsettings.json` to configure devices and sensors.

## Switch to Mock Cloud

For testing without cloud connection:

```csharp
var app = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud()  // Add this line
    .Build();
```
