using Prometheus;

// This sample demonstrates how to publish data from the .NET Meters API as Prometheus metrics.
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

// Suppress other default metrics to expose a cleaner sample data set with only the .NET Meters API data.
Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions
{
    SuppressProcessMetrics = true,
    SuppressEventCounters = true,
    SuppressDebugMetrics = true
});

// Example of static labels that conflict with .NET Meters API labels ("Bytes considered" histogram).
// Static labels overwrite values exported from the .NET Meters API, to resolve conflicting data.
Metrics.DefaultRegistry.SetStaticLabels(new Dictionary<string, string>
{
    { "is_faulted", "false" }
});

// Start the metrics server on your preferred port number.
using var server = new KestrelMetricServer(port: 1234);
server.Start();

// Start publishing sample data via .NET Meters API. All data from the .NET Meters API is published by default.
CustomDotNetMeters.PublishSampleData();

// Metrics published in this sample:
// * custom metrics fed into the .NET Meters API from the CustomDotNetMeters class (enabled by default)
Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
Console.WriteLine("Press enter to exit.");
Console.ReadLine();