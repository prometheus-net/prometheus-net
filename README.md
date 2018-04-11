# prometheus-net

This is a .NET library for instrumenting your applications and exporting metrics to [Prometheus](http://prometheus.io/).

The library targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) which supports the following runtimes (and newer):

* .NET Framework 4.6.1
* .NET Core 2.0
* Mono 5.4

## Installation

Nuget package for general use and metrics export via HttpListener or to Pushgateway: [prometheus-net](https://www.nuget.org/packages/prometheus-net)

>Install-Package prometheus-net

Nuget package for ASP.NET Core middleware and stand-alone Kestrel metrics server: [prometheus-net.AspNetCore](https://www.nuget.org/packages/prometheus-net.AspNetCore)

>Install-Package prometheus-net.AspNetCore

## Breaking changes in version 2.0

To make the library easier to maintain and deliver, version 2.0 introduces some breaking changes:

* Target .NET Standard 2.0 and runtimes that support it - .NET Core 2.0 and .NET Framework 4.6.1. Older runtimes are no longer supported.
* Some classes have been renamed. For example, MetricServer used to be Kestrel-based on .NET Core and HttpListener-based on .NET Framework but in 2.0 it always uses HttpListener, with KestrelHttpServer being a Kestrel-specific server.
* Minor breaking API changes to tidy up confusing parts of the API surface and make it easier to integrate the library.
* Removed dependency on Reactive Extensions. Builtin async/await/Task mechanics are now used.
* Removed PerfCounterCollector as it was mostly obsolete and the removal simplifies maintenance work.

If you are migrating from version 1.x, you may need to make minor changes to your code to adjust for these changes.

## Instrumenting

Four types of metric are offered: Counter, Gauge, Summary and Histogram.
See the documentation on [metric types](http://prometheus.io/docs/concepts/metric_types/)
and [instrumentation best practices](http://prometheus.io/docs/practices/instrumentation/#counter-vs.-gauge-vs.-summary)
on how to use them.

### Counter

Counters go up, and reset when the process restarts.


```csharp
var counter = Metrics.CreateCounter("myCounter", "some help about this");
counter.Inc(5.5);
```

### Gauge

Gauges can go up and down.


```csharp
var gauge = Metrics.CreateGauge("gauge", "help text");
gauge.Inc(3.4);
gauge.Dec(2.1);
gauge.Set(5.3);
```

### Summary

Summaries track the size and number of events.

```csharp
var summary = Metrics.CreateSummary("mySummary", "help text");
summary.Observe(5.3);
```

### Histogram

Histograms track the size and number of events in buckets.
This allows for aggregatable calculation of quantiles.

```csharp
var hist = Metrics.CreateHistogram("my_histogram", "help text", new HistogramConfiguration
{
    Buckets = new[] { 0, 0.2, 0.4, 0.6, 0.8, 0.9 }
});
hist.Observe(0.4);
```

The default buckets (used when you do not specify your own) are intended to cover a typical web/rpc request from milliseconds to seconds.

### Labels

All metrics can have labels, allowing grouping of related time series.

See the best practices on [naming](http://prometheus.io/docs/practices/naming/)
and [labels](http://prometheus.io/docs/practices/instrumentation/#use-labels).

Taking a counter as an example:

```csharp
var counter = Metrics.CreateCounter("myCounter", "help text", new CounterConfiguration
{
    LabelNames = new[] { "method", "endpoint" }
});
counter.WithLabels("GET", "/").Inc();
counter.WithLabels("POST", "/cancel").Inc();
```

## When are metrics published?

Metrics without labels are published immediately. Metrics that use labels are published when you provide the label values.

Sometimes you want to delay publishing a metric until you have loaded some data and have a meaningful value to supply for it. The API allows you to suppress publishing of the initial value until you decide the time is right.

```csharp
var gauge = Metrics.CreateGauge("logged_in_users ", "help text", new GaugeConfiguration
{
    SuppressInitialValue = true
});

// After setting the value, the metric becomes published.
gauge.Set(LoadSessions().Count);
```

You can also use `.Publish()` on a metric to mark it as ready to be published without modifying the initial value.

## HTTP handler

Metrics are usually exposed over HTTP, to be read by the Prometheus server. The default metric server uses HttpListener to open up an HTTP API for metrics export.

```csharp
var metricServer = new MetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the /metrics URL.

`MetricServer.Start()` may throw an access denied exception on Windows if your user does not have the right to open a web server on the specified port. You can use the *netsh* command to grant yourself the required permissions:

> netsh http add urlacl url=http://+:1234/metrics user=DOMAIN\user

## Pushgateway support

Metrics can be posted to a Pushgateway server over HTTP.

```csharp
var metricServer = new MetricPusher(endpoint: "http://pushgateway.example.org:9091/metrics", job: "some_job");
metricServer.Start();
```

## ASP.NET Core middleware

For projects built with ASP.NET Core, a middleware plugin is provided.

If you use the default Visual Studio project template, modify *Startup.cs* as follows:

```
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    // ...

    app.UseMetricServer();

    app.Run(async (context) =>
    {
        // ...
    });
}
```

Alternatively, if you use a custom project startup cycle, you can add this directly to the WebHostBuilder instance:

```csharp
WebHost.CreateDefaultBuilder()
	.Configure(app => app.UseMetricServer())
	.Build()
	.Run();
```

The default configuration will publish metrics on the /metrics URL.

This functionality is delivered in the `prometheus-net.AspNetCore` NuGet package.

## Kestrel stand-alone server

In some situation, you may theoretically wish to start a stand-alone metric server using Kestrel instead of HttpListener.

```csharp
var metricServer = new KestrelMetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the /metrics URL.

This functionality is delivered in the `prometheus-net.AspNetCore` NuGet package.

## Default metrics

The library provides some sample metrics about the current process out of the box. If these are not desirable you may suppress them by calling `DefaultCollectorRegistry.Instance.Clear()` before registering any of your own metrics.

## On-demand collection

In some scenarios you may want to only collect data when it is requested by Prometheus. To easily implement this scenario prometheus-net provides you the ability to perform on-demand collection by implementing the [IOnDemandCollector interface](Prometheus.NetStandard/Advanced/IOnDemandCollector.cs).

Objects that implement this interface are informed before every collection, allowing you to perform any data updates that are relevant. To register on-demand collectors, use `DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors()`.

For an example implementation, see [OnDemandCollection.cs](Tester.NetFramework/OnDemandCollection.cs).

For even more fine-grained control over exported data you should implement a custom collector (see below).

## Implementing custom collectors

The built-in collectors created via the `Metrics` class helper methods provide a simple way to export basic metric types to Prometheus. To implement more advanced metric collection scenarios you can implement the `ICollector` interface yourself.

For an example, see [ExternalDataCollector.cs](Tester.NetFramework/ExternalDataCollector.cs).