using Prometheus;
using Sample.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

// Enable routing, which is necessary to both:
// 1) capture metadata about gRPC requests, to add to the labels.
// 2) expose /metrics in the same pipeline.
app.UseRouting();

// Capture metrics about received gRPC requests.
app.UseGrpcMetrics();

// Capture metrics about received HTTP requests.
app.UseHttpMetrics();

// The sample gRPC service. Use Sample.Grpc.Client to call this service and capture sample metrics.
app.MapGrpcService<GreeterService>();

app.UseEndpoints(endpoints =>
{
    // Enable the /metrics page to export Prometheus metrics.
    // Open http://localhost:5025/metrics to see the metrics.
    //
    // Metrics published in this sample:
    // * built-in process metrics giving basic information about the .NET runtime (enabled by default)
    // * metrics from .NET Event Counters (enabled by default, updated every 10 seconds)
    // * metrics from .NET Meters (enabled by default)
    // * prometheus-net self-inspection metrics that indicate number of registered metrics/timeseries (enabled by default)
    // * metrics about HTTP requests handled by the web app (configured above)
    // * metrics about gRPC requests handled by the web app (configured above)
    app.MapMetrics();
});

// Access the root URL to generate sample data about non-gRPC HTTP requests handled by the app.
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. Use the Sample.Grpc.Client app to communicate with the service.");

app.Run();
