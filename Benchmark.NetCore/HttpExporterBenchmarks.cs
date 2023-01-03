using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Prometheus;
using Prometheus.HttpMetrics;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
public class HttpExporterBenchmarks
{
    private CollectorRegistry _registry;
    private MetricFactory _factory;
    private HttpInProgressMiddleware _inProgressMiddleware;
    private HttpRequestCountMiddleware _countMiddleware;
    private HttpRequestDurationMiddleware _durationMiddleware;

    [Params(100_000)]
    public int RequestCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        _inProgressMiddleware = new HttpInProgressMiddleware(next => Task.CompletedTask, new HttpInProgressOptions
        {
            Gauge = _factory.CreateGauge("in_progress", "help")
        });
        _countMiddleware = new HttpRequestCountMiddleware(next => Task.CompletedTask, new HttpRequestCountOptions
        {
            Counter = _factory.CreateCounter("count", "help")
        });
        _durationMiddleware = new HttpRequestDurationMiddleware(next => Task.CompletedTask, new HttpRequestDurationOptions
        {
            Histogram = _factory.CreateHistogram("duration", "help")
        });
    }

    // Reuse the same HttpContext for different requests, to not count its overhead in the benchmark.
    private static readonly DefaultHttpContext _httpContext = new();

    [Benchmark]
    public async Task HttpInProgress()
    {
        for (var i = 0; i < RequestCount; i++)
            await _inProgressMiddleware.Invoke(_httpContext);
    }

    [Benchmark]
    public async Task HttpRequestCount()
    {
        for (var i = 0; i < RequestCount; i++)
            await _countMiddleware.Invoke(_httpContext);
    }

    [Benchmark]
    public async Task HttpRequestDuration()
    {
        for (var i = 0; i < RequestCount; i++)
            await _durationMiddleware.Invoke(_httpContext);
    }
}