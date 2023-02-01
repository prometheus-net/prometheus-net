// This sample demonstrates how to integrate prometheus-net into a web app while instructing it
// to export metrics on a dedicated port (e.g. so it can be firewalled off from the internet).
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Start the metrics exporter as a background service.
// Open http://localhost:5167 to see the web app.
// Open http://localhost:1234/metrics to see the metrics.
//
// Metrics published:
// * built-in process metrics giving basic information about the .NET runtime (enabled by default)
// * metrics from .NET Event Counters (enabled by default, updated every 10 seconds)
// * metrics from .NET Meters (enabled by default)
// * metrics about requests handled by the web app (configured below)
builder.Services.AddMetricServer(options =>
{
    options.Port = 1234;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

// Capture metrics about all received HTTP requests.
app.UseHttpMetrics();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
