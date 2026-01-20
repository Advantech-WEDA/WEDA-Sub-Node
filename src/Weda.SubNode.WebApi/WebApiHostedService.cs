using Microsoft.Extensions.Options;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Web;

namespace Weda.SubNode.WebApi;

public class WebApiHostedService(IServiceProvider serviceProvider, IOptions<WebApiOptions> options, ILogger<WebApiHostedService> logger) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly WebApiOptions _options = options.Value;
    private readonly ILogger<WebApiHostedService> _logger = logger;
    private WebApplication? _webApp;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_options.Port}");

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(WebApiHostedService).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IDeviceRegistry>());
        builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IRecordingService>());

        _webApp = builder.Build();

        _webApp.UseSwagger();
        _webApp.UseSwaggerUI();
        _webApp.MapControllers();

        _logger.LogInformation("Starting Web API on port {Port}", _options.Port);
        await _webApp.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_webApp != null)
        {
            await _webApp.StopAsync(cancellationToken);
            await _webApp.DisposeAsync();
        }
    }
}