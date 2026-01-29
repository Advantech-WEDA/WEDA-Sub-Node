using Microsoft.Extensions.DependencyInjection;
using Weda.SubNode.Abstractions.Web;
using Weda.SubNode.Host;

namespace Weda.SubNode.WebApi;

public static class WedaApplicationBuilderExtensions
{
    /// <summary>
    /// Enable Web API with swagger UI for administration and monitoring.
    /// </summary>
    /// <param name="builder">The WedaApplicationBuilder instance</param>
    /// <param name="port">HTTP port for the Web API (default: 2395)</param>
    /// <returns>The builder for chaining</returns>
    public static WedaApplicationBuilder AddWebApi(this WedaApplicationBuilder builder, int port = 2395)
    {
        builder.Services.Configure<WebApiOptions>(options =>
        {
            options.Enabled = true;
            options.Port = port;
        });

        builder.Services.AddHostedService<WebApiHostedService>();

        return builder;
    }
}
