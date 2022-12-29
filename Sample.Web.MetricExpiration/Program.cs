using Microsoft.AspNetCore.Http.Extensions;
using Prometheus;
using Prometheus.HttpMetrics;

// This sample demonstrates how to integrate prometheus-net into a web app.
// 
// NuGet packages required:
// * prometheus-net.AspNetCore

// Let's suppress the default metrics that are built-in, to more easily see the changing metrics output.
Metrics.SuppressDefaultMetrics();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

// Use an auto-expiring variant for all the demo metrics here - they get automatically deleted if not used in the last 60 seconds.
var expiringMetricFactory = Metrics.WithManagedLifetime(expiresAfter: TimeSpan.FromSeconds(60));

// OPTION 1: metric lifetime can be managed by leases, to ensure they do not go away during potentially
// long-running operations but go away quickly when the operation is not running anymore (e.g. "in progress" type metrics).
_ = Task.Run(async delegate
{
    var inProgress = expiringMetricFactory.CreateGauge("long_running_operations_in_progress", "Number of long running operations in progress.", labelNames: new[] { "operation_type" });

    // The metric will not be deleted as long as this lease is kept.
    await inProgress.WithLeaseAsync(async inProgressInstance =>
    {
        // Long-running operation, which we track via the "in progress" gauge.
        using (inProgressInstance.TrackInProgress())
            await Task.Delay(TimeSpan.FromSeconds(30));
    }, "VeryLongOperation");

    // Just to let you know when to look at it.
    Console.WriteLine("Long-running operation has finished.");

    // Done! Now the metric lease will be released and soon, the metric will expire and be removed.
});

// OPTION 2: metrics can auto-extend lifetime whenever their values are updated.
app.UseHttpMetrics(options =>
{
    // Here we do something that is typically a no-no in terms of best practices (and GDPR?): we record every unique URL!
    // We use metric expiration to keep the set of metrics in-memory limited to only recently used URLs, which limits the likelihood
    // of our web server getting DoSed. We will still need a very very beefy metrics database to actually handle so much data,
    // so this is not a good idea even if we manage to bypass the most obvious stumbling block of running out of memory!
    options.AddCustomLabel("url", context => context.Request.GetDisplayUrl());

    options.InProgress.Gauge = expiringMetricFactory.CreateGauge(
            "http_requests_in_progress",
            "The number of requests currently in progress in the ASP.NET Core pipeline. One series without controller/action label values counts all in-progress requests, with separate series existing for each controller-action pair.",
            // Let's say that we have all the labels present, as automatic label set selection does not work if we use a custom metric.
            labelNames: HttpRequestLabelNames.All
                    // ... except for "Code" which is only possible to identify after the request is already finished.
                    .Except(new[] { "code" })
                    // ... plus the custom "url" label that we defined above.
                    .Concat(new[] { "url" })
                    .ToArray())
        .WithExtendLifetimeOnUse();

    options.RequestCount.Counter = expiringMetricFactory.CreateCounter(
            "http_requests_received_total",
            "Provides the count of HTTP requests that have been processed by the ASP.NET Core pipeline.",
            // Let's say that we have all the labels present, as automatic label set selection does not work if we use a custom metric.
            labelNames: HttpRequestLabelNames.All
                    // ... plus the custom "url" label that we defined above.
                    .Concat(new[] { "url" })
                    .ToArray())
        .WithExtendLifetimeOnUse();

    options.RequestDuration.Histogram = expiringMetricFactory.CreateHistogram(
            "http_request_duration_seconds",
            "The duration of HTTP requests processed by an ASP.NET Core application.",
            // Let's say that we have all the labels present, as automatic label set selection does not work if we use a custom metric.
            labelNames: HttpRequestLabelNames.All
                    // ... plus the custom "url" label that we defined above.
                    .Concat(new[] { "url" })
                    .ToArray(),
            new HistogramConfiguration
            {
                // 1 ms to 32K ms buckets
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            })
        .WithExtendLifetimeOnUse();
});

app.UseAuthorization();

app.MapRazorPages();

app.UseEndpoints(endpoints =>
{
    // Enable the /metrics page to export Prometheus metrics.
    // Open http://localhost:5283/metrics to see the metrics.
    //
    // Metrics published in this sample:
    // * metrics about requests handled by the web app (configured above)
    // * custom metrics about long-running operations, defined above
    //
    // To try out the expiration feature, navigate to different pages of the app (e.g. between Home and Privacy) and
    // observe how the metrics for accessed URLs disappear a minute after the URL was last used.
    endpoints.MapMetrics();
});

app.Run();
