using Prometheus;

// This sample demonstrates how to publish data from the .NET Meters API as Prometheus metrics.
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

// Suppress other default metrics to expose a cleaner sample data set with only the .NET Meters API data.
Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions
{
    SuppressProcessMetrics = true,
    SuppressEventCounters = true
});

// Start the metrics server on your preferred port number.
using var server = new KestrelMetricServer(port: 1234);
server.Start();

// Start publishing sample data via .NET Meters API. Data from this API is published by default by prometheus-net.
CustomDotNetMeters.PublishSampleData();

// Metrics published in this sample:
// * custom metrics fed into the .NET Meters API from the CustomDotNetMeters class (enabled by default)
Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
Console.WriteLine("Press enter to exit.");
Console.ReadLine();