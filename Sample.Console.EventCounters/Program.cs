using Prometheus;

// This sample demonstrates how to integrate prometheus-net into a console app (e.g. a worker service).
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

// Suppress the default metrics to expose a cleaner sample data set with only the .NET Meters API data.
Metrics.SuppressDefaultMetrics();

// Start the metrics server on your preferred port number.
using var server = new KestrelMetricServer(port: 1234);
server.Start();

// Start publishing data from the .NET event counters.
EventCounterAdapter.StartListening();

// Metrics published in this sample:
// * built-in event counters giving information about the .NET runtime.
Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
Console.WriteLine("Press enter to exit.");
Console.ReadLine();