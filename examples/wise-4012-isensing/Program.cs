using Serilog;
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Wise4012ISensingExample;

try
{
    await using var subNode = new SubNode(WedaApplicationContext.Default);
    subNode.AddDevice(new MyFirstISensingDevice(subNode.Context, "MyFirstDevice"));

    await subNode.InitializeAsync();
    await subNode.StartAsync();

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
