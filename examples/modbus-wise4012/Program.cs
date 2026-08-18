using Serilog;
using Weda.SubNode.Host;
using Wise4012Example;

try
{
    // Use WedaApplication builder pattern with MockCloud for local testing
    var builder = WedaApplication.CreateDefaultBuilder(args);

    builder.AddDevice<MyFirstDevice>("MyFirstDevice");

    var app = builder.Build();

    await app.RunAsync();

    Log.Information("SubNode started. Press Ctrl+C to stop...");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);
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
