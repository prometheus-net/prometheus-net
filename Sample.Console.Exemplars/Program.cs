using Prometheus;
using System.Diagnostics;

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
var recordSizeInPages = Metrics.CreateHistogram("sample_record_size_pages", "Size of a record, in pages.", new HistogramConfiguration
{
    Buckets = Histogram.PowersOfTenDividedBuckets(0, 2, 10)
});

var totalSleepTime = Metrics.CreateCounter("sample_sleep_seconds_total", "Total amount of time spent sleeping.");

// The key from an exemplar key-value pair should be created once and reused to minimize memory allocations.
var recordIdKey = Exemplar.Key("record_id");

_ = Task.Run(async delegate
{
    while (true)
    {
        // Pretend to process a record approximately every second, just for changing sample data.
        var recordId = Guid.NewGuid();
        var recordPageCount = Random.Shared.Next(minValue: 5, maxValue: 100);

        // We pass the record ID key-value pair when we increment the metric.
        // When the metric data is published to Prometheus, the most recent record ID will be attached to it.
        var recordIdKeyValuePair = recordIdKey.WithValue(recordId.ToString());

        recordsProcessed.Inc(recordIdKeyValuePair);
        recordSizeInPages.Observe(recordPageCount, recordIdKeyValuePair);

        // The activity is often automatically inherited from incoming HTTP requests if using OpenTelemetry tracing in ASP.NET Core.
        // Here, we manually create and start an activity for sample purposes, without relying on the platform managing the activity context.
        // See https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts
        using (var activity = new Activity("Taking a break from record processing").Start())
        {
            var sleepStopwatch = Stopwatch.StartNew();
            await Task.Delay(TimeSpan.FromSeconds(1));

            // If you do not specify an exemplar yourself, the trace_id and span_id from the current Activity are automatically used.
            totalSleepTime.Inc(sleepStopwatch.Elapsed.TotalSeconds);
        }
    }
});

// Metrics published in this sample:
// * the custom sample metrics defined above, with exemplars
// * internal debug metrics from prometheus-net, without exemplars
Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
Console.WriteLine("Press enter to exit.");
Console.ReadLine();