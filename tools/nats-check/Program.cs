using NATS.Client.Core;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud.Nats;

// Parse args: nats-check <url> [--user <user> --pass <pass>] [--token <token>] [--creds <file>]
var settings = ParseArgs(args);

Console.WriteLine("=== WedaNode (NATS) Connection Test ===");
Console.WriteLine($"URL:  {settings.Url}");
Console.WriteLine($"Auth: {settings.AuthStrategy}");
Console.WriteLine();

var natsOpts = NatsOpts.Default with
{
    Url = settings.Url,
    AuthOpts = settings.BuildAuthOpts(),
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(5),
};

try
{
    Console.Write("[1/2] Connecting to NATS server... ");
    await using var client = new NatsClient(natsOpts);
    await client.ConnectAsync();
    Console.WriteLine("OK");

    Console.Write("[2/2] Ping... ");
    var rtt = await client.PingAsync();
    Console.WriteLine($"OK (RTT: {rtt.TotalMilliseconds:F1}ms)");

    Console.WriteLine();
    Console.WriteLine($"Server: {client.Connection.ServerInfo?.Name ?? "unknown"}");
    Console.WriteLine($"Version: {client.Connection.ServerInfo?.Version ?? "unknown"}");

    Console.WriteLine();
    Console.WriteLine("=== RESULT: NATS connection OK ===");
    return 0;
}
catch (NatsException ex)
{
    Console.WriteLine("FAIL");
    Console.WriteLine();
    Console.Error.WriteLine($"NATS error: {ex.Message}");
    PrintDiagnostics(settings);
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine("FAIL");
    Console.WriteLine();
    Console.Error.WriteLine($"Error: {ex.Message}");
    PrintDiagnostics(settings);
    return 1;
}

static void PrintDiagnostics(NatsConnectionSettings settings)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Possible causes:");

    switch (settings.AuthStrategy)
    {
        case NatsAuthStrategy.UserPassword:
            Console.Error.WriteLine("  - Invalid username or password");
            Console.Error.WriteLine("  - Auth strategy mismatch (server expects different auth)");
            break;
        case NatsAuthStrategy.Token:
            Console.Error.WriteLine("  - Invalid or expired token");
            break;
        case NatsAuthStrategy.CredFile:
            Console.Error.WriteLine("  - Invalid credential file or expired JWT");
            break;
        default:
            Console.Error.WriteLine("  - Server requires authentication");
            break;
    }

    Console.Error.WriteLine("  - NATS server is not running");
    Console.Error.WriteLine("  - Firewall or network issue");
    Console.Error.WriteLine("  - Incorrect URL");
    Console.Error.WriteLine();
    Console.Error.WriteLine("RECOMMENDATION: Use mock cloud (Cloud.Mock()) for development");
}

static NatsConnectionSettings ParseArgs(string[] args)
{
    var settings = new NatsConnectionSettings();

    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: dotnet run --project tools/nats-check -- <nats-url> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --user <username>   Username for UserPassword auth");
        Console.Error.WriteLine("  --pass <password>   Password for UserPassword auth");
        Console.Error.WriteLine("  --token <token>     Token for Token auth");
        Console.Error.WriteLine("  --creds <file>      Credential file path for CredFile auth");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  dotnet run -- nats://localhost:4222");
        Console.Error.WriteLine("  dotnet run -- nats://10.0.0.1:4222 --user admin --pass secret");
        Environment.Exit(1);
    }

    settings.Url = args[0];

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--user" when i + 1 < args.Length:
                settings.Username = args[++i];
                settings.AuthStrategy = NatsAuthStrategy.UserPassword;
                break;
            case "--pass" when i + 1 < args.Length:
                settings.Password = args[++i];
                settings.AuthStrategy = NatsAuthStrategy.UserPassword;
                break;
            case "--token" when i + 1 < args.Length:
                settings.Token = args[++i];
                settings.AuthStrategy = NatsAuthStrategy.Token;
                break;
            case "--creds" when i + 1 < args.Length:
                settings.CredFile = args[++i];
                settings.AuthStrategy = NatsAuthStrategy.CredFile;
                break;
        }
    }

    return settings;
}
