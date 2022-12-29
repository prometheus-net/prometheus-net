using Prometheus;

// This sample demonstrates how to attach exemplars to metrics exposed by a .NET console app.
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

// Suppress some default metrics to make the output cleaner, so the exemplars are easier to see.
Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions
{
    SuppressEventCounters = true,
    SuppressMeters = true,
    SuppressProcessMetrics = true
});

// Start the metrics server on your preferred port number.
using var server = new KestrelMetricServer(port: 1234);
server.Start();

// Generate some sample data from fake business logic.
var recordsProcessed = Metrics.CreateCounter("sample_records_processed_total", "Total number of records processed.");

// The key from an exemplar key-value pair should be created once and reused to minimize memory allocations.
var recordIdKey = Exemplar.Key("record_id");

_ = Task.Run(async delegate
{
    while (true)
    {
        // Pretend to process a record approximately every second, just for changing sample data.
        var recordId = Guid.NewGuid();

        // We pass the record ID key-value pair when we increment the metric.
        // When the metric data is published to Prometheus, the most recent record ID will be attached to it.
        var exemplar = recordIdKey.WithValue(recordId.ToString());
        recordsProcessed.Inc(exemplar);

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
});

// Metrics published in this sample:
// * the custom sample counter defined above, with exemplars
// * internal debug metrics from prometheus-net, without exemplars
Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
Console.WriteLine("Press enter to exit.");
Console.ReadLine();