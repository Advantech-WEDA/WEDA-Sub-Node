using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Weda.SubNode.Simulators.Modbus;
using Weda.SubNode.Simulators.Mqtt;
using Weda.SubNode.Simulators.WebSocket;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // Register simulators based on which config sections are present
        if (config.GetSection(TcpModbusSimulatorConfiguration.SectionName).Exists())
        {
            services.AddSingleton(sp =>
                config.GetSection(TcpModbusSimulatorConfiguration.SectionName)
                    .Get<TcpModbusSimulatorConfiguration>()
                ?? throw new InvalidOperationException(
                    $"Invalid {TcpModbusSimulatorConfiguration.SectionName} configuration"));

            services.AddHostedService<TcpModbusSimulatorHostedService>();
        }

        if (config.GetSection(WebSocketSimulatorConfiguration.SectionName).Exists())
        {
            services.AddSingleton(sp =>
                config.GetSection(WebSocketSimulatorConfiguration.SectionName)
                    .Get<WebSocketSimulatorConfiguration>()
                ?? throw new InvalidOperationException(
                    $"Invalid {WebSocketSimulatorConfiguration.SectionName} configuration"));

            services.AddHostedService<WebSocketSimulatorHostedService>();
        }

        if (config.GetSection(MqttImageSimulatorConfiguration.SectionName).Exists())
        {
            services.AddSingleton(sp =>
                config.GetSection(MqttImageSimulatorConfiguration.SectionName)
                    .Get<MqttImageSimulatorConfiguration>()
                ?? throw new InvalidOperationException(
                    $"Invalid {MqttImageSimulatorConfiguration.SectionName} configuration"));

            services.AddHostedService<MqttImageSimulatorHostedService>();
        }
    });

await builder.Build().RunAsync();
