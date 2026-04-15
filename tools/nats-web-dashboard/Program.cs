using NatsWebDashboard.Configuration;
using NatsWebDashboard.Hubs;
using NatsWebDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<DashboardOptions>(
    builder.Configuration.GetSection(DashboardOptions.SectionName));

// --- Services ---
builder.Services.AddSignalR();
builder.Services.AddHostedService<NatsSubscriptionService>();

var app = builder.Build();

// --- Middleware pipeline ---
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<TelemetryHub>("/hub");

// --- Start ---
var options = builder.Configuration
    .GetSection(DashboardOptions.SectionName)
    .Get<DashboardOptions>() ?? new DashboardOptions();

app.Logger.LogInformation("Dashboard running at http://localhost:{Port}", options.Port);
app.Run($"http://0.0.0.0:{options.Port}");
