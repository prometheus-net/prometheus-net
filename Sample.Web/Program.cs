using Prometheus;
using Sample.Web;

// This sample demonstrates how to integrate prometheus-net into a web app.
// 
// NuGet packages required:
// * prometheus-net.AspNetCore
// * prometheus-net.AspNetCore.HealthChecks (optional; for publishing health check results)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Define an HTTP client that reports metrics about its usage, to be used by a sample background service.
builder.Services.AddHttpClient(SampleService.HttpClientName);

// Export metrics from all HTTP clients registered in services
builder.Services.UseHttpClientMetrics();

// A sample service that uses the above HTTP client.
builder.Services.AddHostedService<SampleService>();

builder.Services.AddHealthChecks()
    // Define a sample health check that always signals healthy state.
    .AddCheck<SampleHealthCheck>(nameof(SampleHealthCheck))
    // Report health check results in the metrics output.
    .ForwardToPrometheus();

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

app.UseEndpoints(endpoints =>
{
    // Enable the /metrics page to export Prometheus metrics.
    // Open http://localhost:5099/metrics to see the metrics.
    //
    // Metrics published in this sample:
    // * built-in process metrics giving basic information about the .NET runtime (enabled by default)
    // * metrics from .NET Event Counters (enabled by default, updated every 10 seconds)
    // * metrics from .NET Meters (enabled by default)
    // * metrics about requests made by registered HTTP clients used in SampleService (configured above)
    // * metrics about requests handled by the web app (configured above)
    // * ASP.NET health check statuses (configured above)
    // * custom business logic metrics published by the SampleService class
    endpoints.MapMetrics();
});

app.Run();
