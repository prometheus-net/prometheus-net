# prometheus-net

This is a .NET library for instrumenting your applications and exporting metrics to [Prometheus](http://prometheus.io/).

The library targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) which supports the following runtimes (and newer):

* .NET Framework 4.6.1
* .NET Core 2.0
* Mono 5.4

# Best practices and usage

This documentation is only a minimal quick start. For detailed guidance on using Prometheus in your solutions, refer to the [prometheus-users discussion group](https://groups.google.com/forum/#!forum/prometheus-users). You are also expected to be familiar with the [Prometheus user guide](https://prometheus.io/docs/introduction/overview/).

Four types of metrics are offered: Counter, Gauge, Summary and Histogram.

See the documentation on [metric types](http://prometheus.io/docs/concepts/metric_types/)
and [instrumentation best practices](http://prometheus.io/docs/practices/instrumentation/#counter-vs.-gauge-vs.-summary)
to learn what each is good for.

The most common practice in C# code is to have a `static readonly` field for each metric that you wish to export from a given class.

More complex patterns may also be used (e.g. combining with dependency injection). The library is quite tolerant of different usage models - if the API allows it, it will generally work fine and provide satisfactory performance. The library is thread-safe.

# Installation

Nuget package for general use and metrics export via HttpListener or to Pushgateway: [prometheus-net](https://www.nuget.org/packages/prometheus-net)

>Install-Package prometheus-net

Nuget package for ASP.NET Core middleware and stand-alone Kestrel metrics server: [prometheus-net.AspNetCore](https://www.nuget.org/packages/prometheus-net.AspNetCore)

>Install-Package prometheus-net.AspNetCore

## Default metrics

The library provides some sample metrics about the current process out of the box, simply to ensure that some output is produced in a default configuration. If these metrics are not desirable you may suppress them by calling `DefaultCollectorRegistry.Instance.Clear()` before registering any of your own metrics.

# Counters

Counters only increase in value and reset to zero when the process restarts.

```csharp
private static readonly ProcessedJobCount =
	Metrics.CreateCounter("myapp_jobs_processed_total", "Number of processed jobs.");

...

ProcessJob();
ProcessedJobCount.Inc();
```

# Gauges

Gauges can have any numeric value and change arbitrarily.

```csharp
private static readonly JobsInQueue
	= Metrics.CreateGauge("myapp_jobs_queued", "Number of jobs waiting for processing in the queue.");

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
private static readonly Summary RequestSizeSummary =
	Metrics.CreateSummary("myapp_request_size_bytes", "Summary of request sizes (in bytes) over last 10 minutes.");

...

RequestSizeSummary.Observe(request.Length);
```

# Histogram

Histograms track the size and number of events in buckets. This allows for aggregatable calculation of quantiles.

```csharp
private static readonly Histogram OrderValueHistogram =
	Metrics.CreateHistogram("myapp_order_value_usd", "Histogram of received order values (in USD).",
		new HistogramConfiguration
		{
			// We divide measurements in 10 buckets of $100 each, up to $1000.
			Buckets = Histogram.LinearBuckets(start: 1, width: 100, count: 10)
		});

...

OrderValueHistogram.Observe(order.TotalValueUsd);
```

# Timers

Timers can be used to report the duration of an action (in seconds) to a Summary, Histogram or Gauge. Wrap the action you want to measure in a using statement.

```csharp
private static readonly Histogram LoginDuration =
	Metrics.CreateHistogram("myapp_login_duration_seconds", "Histogram of login call processing durations.");

...

using (LoginDuration.NewTimer())
{
    IdentityManager.AuthenticateUser(Request.Credentials);
}
```

# Labels

All metrics can have labels, allowing grouping of related time series.

See the best practices on [naming](http://prometheus.io/docs/practices/naming/)
and [labels](http://prometheus.io/docs/practices/instrumentation/#use-labels).

Taking a counter as an example:

```csharp
private static readonly RequestCountByMethod = Metrics.CreateCounter("myapp_requests_total", "Number of requests received, by HTTP method.",
	new CounterConfiguration
	{
		// Here you specify only the names of the labels.
		LabelNames = new[] { "method" }
	});

...

// You can specify the values for the labels later, once you know the right values (e.g in your request handler code).
counter.WithLabels("GET").Inc();
```

NB! Best practices of metric design is to minimize the number of different label values. HTTP request method is good - there are not many values. However, URL would be a bad choice for labeling - it has too many possible values and would lead to significant data processing inefficiency. Try to minimize the possible number of label values in your metric model.

# When are metrics published?

Metrics without labels are published immediately after the `Metrics.CreateX()` call. Metrics that use labels are published when you provide the label values for the first time.

Sometimes you want to delay publishing a metric until you have loaded some data and have a meaningful value to supply for it. The API allows you to suppress publishing of the initial value until you decide the time is right.

```csharp
private static readonly UsersLoggedIn = Metrics.CreateGauge("myapp_users_logged_in", "Number of active user sessions",
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

# ASP.NET Core HTTP request metrics

The library provides some metrics for ASP.NET Core applications:

* Total number of 'in-flight' (i.e. currently executing) requests.
* Total number of HTTP requests.
* Duration of HTTP requests.

These metrics include labels for status code, HTTP method, ASP.NET Controller and ASP.NET Action.

You can register all of the metrics using the default labels and names as follows:

```csharp
// In your Startup.cs Configure() method
app.UseHttpExporter();
```

If you wish to provide a custom Metric for each of the metrics, or disable certain metrics, you can configure the Http Exporter like this:

```csharp
app.UseHttpExporter(options =>
{
	options.RequestCount.Enabled = false;

	options.RequestDuration.Histogram = Metrics.CreateHistogram("my_custom_name", "my_custom_help", Histogram.LinearBuckets(0.1, 1, 100), "code", "method");
});
```

The labels for the custom metric you provide *must* be a subset of the following:
* "code" - Status Code
* "method" - HTTP method
* "controller" - ASP.NET Core Controller
* "action" - ASP.NET Core Action

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

# Kestrel stand-alone server

In some situation, you may theoretically wish to start a stand-alone metric server using Kestrel instead of HttpListener.

```csharp
var metricServer = new KestrelMetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the /metrics URL.

This functionality is delivered in the `prometheus-net.AspNetCore` NuGet package.

# Publishing to Pushgateway

Metrics can be posted to a [Pushgateway](https://prometheus.io/docs/practices/pushing/) server over HTTP.

```csharp
var metricServer = new MetricPusher(endpoint: "http://pushgateway.example.org:9091/metrics", job: "some_job");
metricServer.Start();
```

# Publishing via standalone HTTP handler

Metrics are usually exposed over HTTP, to be read by the Prometheus server. The default metric server uses HttpListener to open up an HTTP API for metrics export.

```csharp
var metricServer = new MetricServer(port: 1234);
metricServer.Start();
```

The default configuration will publish metrics on the /metrics URL.

`MetricServer.Start()` may throw an access denied exception on Windows if your user does not have the right to open a web server on the specified port. You can use the *netsh* command to grant yourself the required permissions:

> netsh http add urlacl url=http://+:1234/metrics user=DOMAIN\user

# On-demand collection

In some scenarios you may want to only collect data when it is requested by Prometheus. To easily implement this scenario prometheus-net provides you the ability to perform on-demand collection by implementing the [IOnDemandCollector interface](Prometheus.NetStandard/Advanced/IOnDemandCollector.cs).

Objects that implement this interface are informed before every collection, allowing you to perform any data updates that are relevant. To register on-demand collectors, use `DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors()`.

For an example implementation, see [OnDemandCollection.cs](Tester.NetFramework/OnDemandCollection.cs).

For even more fine-grained control over exported data you should implement a custom collector (see below).

# Implementing custom collectors

The built-in collectors created via the `Metrics` class helper methods provide a simple way to export basic metric types to Prometheus. To implement more advanced metric collection scenarios you can implement the `ICollector` interface yourself.

For an example, see [ExternalDataCollector.cs](Tester.NetFramework/ExternalDataCollector.cs).

# Related projects

* [prometheus-net.DotNetRuntime](https://github.com/djluck/prometheus-net.DotNetRuntime) instruments .NET Core 2.2 apps to export metrics on .NET Core performance.
