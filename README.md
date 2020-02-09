# prometheus-net

This is a .NET library for instrumenting your applications and exporting metrics to [Prometheus](http://prometheus.io/).

[![Build status](https://dev.azure.com/prometheus-net/prometheus-net/_apis/build/status/prometheus-net)](https://dev.azure.com/prometheus-net/prometheus-net/_build/latest?definitionId=1) [![Nuget](https://img.shields.io/nuget/v/prometheus-net.svg)](https://www.nuget.org/packages/prometheus-net/) ![Nuget](https://img.shields.io/nuget/dt/prometheus-net.svg)

![](Screenshot.png)

The library targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) which supports the following runtimes (and newer):

* .NET Framework 4.6.1
* .NET Core 2.0
* Mono 5.4

The ASP.NET Core specific functionality requires ASP.NET Core 2.1 or newer. The .NET Core specific functionality requires .NET Core 2.1 or newer.

Related projects:

* [prometheus-net.DotNetRuntime](https://github.com/djluck/prometheus-net.DotNetRuntime) instruments .NET Core apps to export metrics on .NET Core performance.
* [prometheus-net.AspNet](https://github.com/rocklan/prometheus-net.AspNet) instruments ASP.NET full framework apps to export metrics on performance. 
* [prometheus-net/docker_exporter](https://github.com/prometheus-net/docker_exporter) exports metrics about a Docker installations.
* [prometheus-net/tzsp_packetstream_exporter](https://github.com/prometheus-net/tzsp_packetstream_exporter) exports metrics about the data flows found in a stream of IPv4 packets.
* [prometheus-net Grafana dashboards](https://github.com/prometheus-net/grafana-dashboards) provides example dashboards for visualizing prometheus-net metrics in [Grafana](https://grafana.com/).


# Table of contents

* [Best practices and usage](#best-practices-and-usage)
* [Installation](#installation)
* [Quick start](#quick-start)
* [Counters](#counters)
* [Gauges](#gauges)
* [Summary](#summary)
* [Histogram](#histogram)
* [Measuring operation duration](#measuring-operation-duration)
* [Tracking in-progress operations](#tracking-in-progress-operations)
* [Counting exceptions](#counting-exceptions)
* [Labels](#labels)
* [When are metrics published?](#when-are-metrics-published)
* [ASP.NET Core exporter middleware](#aspnet-core-exporter-middleware)
* [ASP.NET Core HTTP request metrics](#aspnet-core-http-request-metrics)
* [ASP.NET Core with basic authentication](#aspnet-core-with-basic-authentication)
* [ASP.NET Web API exporter](#aspnet-web-api-exporter)
* [Kestrel stand-alone server](#kestrel-stand-alone-server)
* [Publishing to Pushgateway](#publishing-to-pushgateway)
* [Publishing to Pushgateway with basic authentication](#publishing-to-pushgateway-with-basic-authentication)
* [Publishing via standalone HTTP handler](#publishing-via-standalone-http-handler)
* [Publishing raw metrics document](#publishing-raw-metrics-document)
* [Just-in-time updates](#just-in-time-updates)
* [Suppressing default metrics](#suppressing-default-metrics)
* [DiagnosticSource integration](#diagnosticsource-integration)

# Best practices and usage

This library allows you to instrument your code with custom metrics and provides some built-in metric collection integrations for ASP.NET Core.

The documentation here is only a minimal quick start. For detailed guidance on using Prometheus in your solutions, refer to the [prometheus-users discussion group](https://groups.google.com/forum/#!forum/prometheus-users). You are also expected to be familiar with the [Prometheus user guide](https://prometheus.io/docs/introduction/overview/).

Four types of metrics are available: Counter, Gauge, Summary and Histogram. See the documentation on [metric types](http://prometheus.io/docs/concepts/metric_types/) and [instrumentation best practices](http://prometheus.io/docs/practices/instrumentation/#counter-vs.-gauge-vs.-summary) to learn what each is good for.

**The `Metrics` class is the main entry point to the API of this library.** The most common practice in C# code is to have a `static readonly` field for each metric that you wish to export from a given class.

More complex patterns may also be used (e.g. combining with dependency injection). The library is quite tolerant of different usage models - if the API allows it, it will generally work fine and provide satisfactory performance. The library is thread-safe.

# Installation

Nuget package for general use and metrics export via HttpListener or to Pushgateway: [prometheus-net](https://www.nuget.org/packages/prometheus-net)

>Install-Package prometheus-net

Nuget package for ASP.NET Core middleware and stand-alone Kestrel metrics server: [prometheus-net.AspNetCore](https://www.nuget.org/packages/prometheus-net.AspNetCore)

>Install-Package prometheus-net.AspNetCore

Nuget package for ASP.NET Web API middleware on .NET Framework: [prometheus-net.NetFramework.AspNet](https://www.nuget.org/packages/prometheus-net.NetFramework.AspNet)

>Install-Package prometheus-net.NetFramework.AspNet


# Quick start

After installing the library, you should:

1. Initialize some metrics and start updating their values.
1. Publish the collected metrics over HTTP.
1. Configure the Prometheus server to poll your app for metrics on regular intervals.

The chapters below describe the various ways you can initialize or update metrics and the ways in which they can be published.

The following is a minimal implementation that simply increments a counter once a second, publishing the metrics on http://localhost:1234/metrics

```csharp
using Prometheus;
using System;
using System.Threading;

class Program
{
    private static readonly Counter TickTock =
        Metrics.CreateCounter("sampleapp_ticks_total", "Just keeps on ticking");

    static void Main()
    {
        var server = new MetricServer(hostname: "localhost", port: 1234);
        server.Start();

        while (true)
        {
            TickTock.Inc();
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }
}
```

# Counters

Counters only increase in value and reset to zero when the process restarts.

```csharp
private static readonly Counter ProcessedJobCount = Metrics
    .CreateCounter("myapp_jobs_processed_total", "Number of processed jobs.");

...

ProcessJob();
ProcessedJobCount.Inc();
```

# Gauges

Gauges can have any numeric value and change arbitrarily.

```csharp
private static readonly Gauge JobsInQueue = Metrics
    .CreateGauge("myapp_jobs_queued", "Number of jobs waiting for processing in the queue.");

...

jobQueue.Enqueue(job);
JobsInQueue.Inc();

...

var job = jobQueue.Dequeue();
JobsInQueue.Dec();
```

# Summary

Summaries track the trends in events over time (10 minutes by default).

```csharp
private static readonly Summary RequestSizeSummary = Metrics
    .CreateSummary("myapp_request_size_bytes", "Summary of request sizes (in bytes) over last 10 minutes.");

...

RequestSizeSummary.Observe(request.Length);
```

By default, only the sum and total count are reported. You may also specify quantiles to measure:

```csharp
private static readonly Summary RequestSizeSummary = Metrics
    .CreateSummary("myapp_request_size_bytes", "Summary of request sizes (in bytes) over last 10 minutes.",
        new SummaryConfiguration
        {
            Objectives = new[]
            {
                new QuantileEpsilonPair(0.5, 0.05),
                new QuantileEpsilonPair(0.9, 0.05),
                new QuantileEpsilonPair(0.95, 0.01),
                new QuantileEpsilonPair(0.99, 0.005),
            }
        });
```

The epsilon indicates the absolute error allowed in measurements. For more information, refer to the [Prometheus documentation on summaries and histograms](https://prometheus.io/docs/practices/histograms/).

# Histogram

Histograms track the size and number of events in buckets. This allows for aggregatable calculation of quantiles.

```csharp
private static readonly Histogram OrderValueHistogram = Metrics
    .CreateHistogram("myapp_order_value_usd", "Histogram of received order values (in USD).",
        new HistogramConfiguration
        {
            // We divide measurements in 10 buckets of $100 each, up to $1000.
            Buckets = Histogram.LinearBuckets(start: 100, width: 100, count: 10)
        });

...

OrderValueHistogram.Observe(order.TotalValueUsd);
```

# Measuring operation duration

Timers can be used to report the duration of an operation (in seconds) to a Summary, Histogram, Gauge or Counter. Wrap the operation you want to measure in a using block.

```csharp
private static readonly Histogram LoginDuration = Metrics
    .CreateHistogram("myapp_login_duration_seconds", "Histogram of login call processing durations.");

...

using (LoginDuration.NewTimer())
{
    IdentityManager.AuthenticateUser(Request.Credentials);
}
```

# Tracking in-progress operations

You can use `Gauge.TrackInProgress()` to track how many concurrent operations are taking place. Wrap the operation you want to track in a using block.

```csharp
private static readonly Gauge DocumentImportsInProgress = Metrics
    .CreateGauge("myapp_document_imports_in_progress", "Number of import operations ongoing.");

...

using (DocumentImportsInProgress.TrackInProgress())
{
    DocumentRepository.ImportDocument(path);
}
```

# Counting exceptions

You can use `Counter.CountExceptions()` to count the number of exceptions that occur while executing some code.


```csharp
private static readonly Counter FailedDocumentImports = Metrics
    .CreateCounter("myapp_document_imports_failed_total", "Number of import operations that failed.");

...

FailedDocumentImports.CountExceptions(() => DocumentRepository.ImportDocument(path));
```

You can also filter the exception types to observe:

```csharp
FailedDocumentImports.CountExceptions(() => DocumentRepository.ImportDocument(path), IsImportRelatedException);

bool IsImportRelatedException(Exception ex)
{
    // Do not count "access denied" exceptions - those are user error for pointing us to a forbidden file.
    if (ex is UnauthorizedAccessException)
        return false;

    return true;
}
```

# Labels

All metrics can have labels, allowing grouping of related time series.

See the best practices on [naming](http://prometheus.io/docs/practices/naming/)
and [labels](http://prometheus.io/docs/practices/instrumentation/#use-labels).

Taking a counter as an example:

```csharp
private static readonly Counter RequestCountByMethod = Metrics
    .CreateCounter("myapp_requests_total", "Number of requests received, by HTTP method.",
        new CounterConfiguration
        {
            // Here you specify only the names of the labels.
            LabelNames = new[] { "method" }
        });

...

// You can specify the values for the labels later, once you know the right values (e.g in your request handler code).
counter.WithLabels("GET").Inc();
```

NB! Best practices of metric design is to **minimize the number of different label values**. For example:

* HTTP request method is a good choice for labeling - there are not many values.
* URL is a bad choice for labeling - it has many possible values and would lead to significant data processing inefficiency.

# When are metrics published?

Metrics without labels are published immediately after the `Metrics.CreateX()` call. Metrics that use labels are published when you provide the label values for the first time.

Sometimes you want to delay publishing a metric until you have loaded some data and have a meaningful value to supply for it. The API allows you to suppress publishing of the initial value until you decide the time is right.

```csharp
private static readonly Gauge UsersLoggedIn = Metrics
    .CreateGauge("myapp_users_logged_in", "Number of active user sessions",
        new GaugeConfiguration
        {
            SuppressInitialValue = true
        });

...

// After setting the value for the first time, the metric becomes published.
UsersLoggedIn.Set(LoadSessions().Count);
```

You can also use `.Publish()` on a metric to mark it as ready to be published without modifying the initial value (e.g. to publish a zero).

# ASP.NET Core exporter middleware

For projects built with ASP.NET Core, a middleware plugin is provided.

If you use the default Visual Studio project template, modify `Startup.cs` as follows:

* ASP.NET Core 3 or newer
    * Add `endpoints.MapMetrics()` to the endpoint configuration under `app.UseEndpoints`.
* ASP.NET Core 2
    * Add `app.UseMetricServer()` to the top of the `Configure` method.

```csharp
public void Configure(IApplicationBuilder app, ...)
{
    // ASP.NET Core 2
    app.UseMetricServer();

    // ...

    // ASP.NET Core 3 or newer
    app.UseEndpoints(endpoints =>
    {
        // ...

        endpoints.MapMetrics();
    };
}
```

The default configuration will publish metrics on the `/metrics` URL.

The ASP.NET Core functionality is delivered in the `prometheus-net.AspNetCore` NuGet package.

# ASP.NET Core HTTP request metrics

The library exposes some metrics from ASP.NET Core applications:

* Number of HTTP requests in progress.
* Total number of received HTTP requests.
* Duration of HTTP requests.

These metrics include labels for status code, HTTP method, Controller and Action.

The ASP.NET Core functionality is delivered in the `prometheus-net.AspNetCore` NuGet package.

You can expose HTTP metrics by modifying your `Startup.Configure()` method:

* ASP.NET Core 3 or newer
    * After `app.UseRouting()` add `app.UseHttpMetrics()`.
* ASP.NET Core 2
    * After `app.UseMetricServer()` add `app.UseHttpMetrics()`.

Example `Startup.cs` (ASP.NET Core 3):

```csharp
public void Configure(IApplicationBuilder app, ...)
{
    // ...

    app.UseRouting();
    app.UseHttpMetrics();

    // ...
}
```

NB! Exception handler middleware that changes HTTP response codes must be registered **after** `UseHttpMetrics()` in order to ensure that prometheus-net reports the correct HTTP response status code.

# ASP.NET Core with basic authentication

You may wish to restrict access to the metrics export URL. This can be accomplished using any ASP.NET Core authentication mechanism, as prometheus-net integrates directly into the composable ASP.NET Core request processing pipeline.

For a simple example we can take [BasicAuthMiddleware by Johan Boström](https://www.johanbostrom.se/blog/adding-basic-auth-to-your-mvc-application-in-dotnet-core) which can be integrated by replacing the `app.UseMetricServer()` line with the following code block:

```csharp
app.Map("/metrics", metricsApp =>
{
    metricsApp.UseMiddleware<BasicAuthMiddleware>("Contoso Corporation");

    // We already specified URL prefix in .Map() above, no need to specify it again here.
    metricsApp.UseMetricServer("");
});
```

# ASP.NET Web API exporter

The easiest way to export metrics from an ASP.NET Web API app on the full .NET Framework is to use `AspNetMetricServer` in your `Global.asax.cs` file:

```csharp
protected void Application_Start(object sender, EventArgs e)
{
    AspNetMetricServer.RegisterRoutes(GlobalConfiguration.Configuration);
}
```

The above snippet exposes metrics on the `/metrics` URL.

The `AspNetMetricServer` class is provided by the `prometheus-net.NetFramework.AspNet` NuGet package.

# Kestrel stand-alone server

In some situation, you may wish to start a stand-alone metric server using Kestrel (e.g. if your app has no other HTTP-accessible functionality).

```csharp
var metricServer = new KestrelMetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the `/metrics` URL.

# Publishing to Pushgateway

Metrics can be posted to a [Pushgateway](https://prometheus.io/docs/practices/pushing/) server.

```csharp
var pusher = new MetricPusher(new MetricPusherOptions
{
    Endpoint = "https://pushgateway.example.org:9091/metrics",
    Job = "some_job"
});

pusher.Start();
```

# Publishing to Pushgateway with basic authentication

You can use a custom HttpClient to supply credentials for the Pushgateway.

```csharp
// Placeholder username and password here - replace with your own data.
var headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("username:password"));
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", headerValue);

var pusher = new MetricPusher(new MetricPusherOptions
{
    Endpoint =  "https://pushgateway.example.org:9091/metrics",
    Job = "some_job",
    HttpClientProvider = () => httpClient
});

pusher.Start();
```

# Publishing via standalone HTTP handler

As a fallback option for scenarios where Kestrel or ASP.NET Core hosting is unsuitable, an `HttpListener` based metrics server implementation is also available.

```csharp
var metricServer = new MetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the `/metrics` URL.

`MetricServer.Start()` may throw an access denied exception on Windows if your user does not have the right to open a web server on the specified port. You can use the *netsh* command to grant yourself the required permissions:

> netsh http add urlacl url=http://+:1234/metrics user=DOMAIN\user

# Publishing raw metrics document

In scenarios where you handle publishing via a custom endpoint, you can export the entire metrics data set as a Prometheus text document.

```csharp
await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(outputStream);
```

# Just-in-time updates

In some scenarios you may want to only collect data when it is requested by Prometheus. To easily implement this scenario prometheus-net enables you to register a callback before every collection occurs. Register your callback using `Metrics.DefaultRegistry.AddBeforeCollectCallback()`.

Every callback will be executed before each collection, which will not finish until every callback has finished executing. Prometheus will expect each scrape to complete within a certain amount of seconds. To avoid timeouts, ensure that any registered callbacks execute quickly.

* A synchronous callback (of type `Action`) should not take more than a few milliseconds. Do not read data from remote systems in these callbacks.
* An asynchronous callback (of type `Func<CancellationToken, Task>`) is more suitable for long-running data collection work (lasting a few seconds). You can use asynchronous callbacks for reading data from remote systems.

```csharp
Metrics.DefaultRegistry.AddBeforeCollectCallback(async (cancel) =>
{
    // Probe a remote system.
    var response = await httpClient.GetAsync("https://google.com", cancel);

    // Increase a counter by however many bytes we loaded.
    googlePageBytes.Inc(response.Content.Headers.ContentLength ?? 0);
});
```

# Suppressing default metrics

The library provides some sample metrics about the current process out of the box, simply to ensure that some output is produced in a default configuration. If these metrics are not desirable you may remove them by calling `Metrics.SuppressDefaultMetrics()` before registering any of your own metrics.

# DiagnosticSource integration

[.NET Core provides the DiagnosticSource mechanism for reporting diagnostic events](https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md), used widely by .NET and ASP.NET Core classes. To expose basic data on these events via Prometheus, you can use the `DiagnosticSourceAdapter` class:

```csharp
// An optional "options" parameter is available to customize adapter behavior.
var diagnosticSourceRegistration = DiagnosticSourceAdapter.StartListening();

...

// Stops listening for DiagnosticSource events.
diagnosticSourceRegistration.Dispose();
```

Any events that occur are exported as Prometheus metrics, indicating the name of the event source and the name of the event:

```
diagnostic_events_total{source="Microsoft.AspNetCore",event="Microsoft.AspNetCore.Mvc.AfterAction"} 4
diagnostic_events_total{source="HttpHandlerDiagnosticListener",event="System.Net.Http.Request"} 8
```

The level of detail obtained from this is rather low - only the total count for each event is exported. For more fine-grained analytics, you need to listen to DiagnosticSource events on your own and create custom metrics that can understand the meaning of each particular type of event that is of interest to you.
