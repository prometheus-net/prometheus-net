using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Prometheus;
using Prometheus.Advanced;
using Prometheus.HttpExporter.AspNetCore.HttpRequestCount;
using Prometheus.HttpExporter.AspNetCore.HttpRequestDuration;
using Prometheus.HttpExporter.AspNetCore.InFlight;

namespace Benchmark.NetFramework
{
    /// <summary>
    /// One pattern advocated by Prometheus documentation is to implement scraping of external systems by
    /// creating a brand new set of metrics for each scrape. So let's benchmark this scenario.
    /// </summary>
    [Config(typeof(MultipleRuntimes))]
    [MemoryDiagnoser]
    public class HttpExporterBenchmarks
    {
        private ICollectorRegistry _registry;
        private MetricFactory _factory;
        private HttpInFlightMiddleware _inFlightMiddleware;
        private HttpRequestCountMiddleware _countMiddleware;
        private HttpRequestDurationMiddleware _durationMiddleware;
        
        private ParallelOptions ParallelOptions  => new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        };

        [Params(100, 1000, 10000)]
        public int RequestCount { get; set; }
        
        [Params(1, 4)]
        public int MaxDegreeOfParallelism { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _registry = new DefaultCollectorRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);

            _inFlightMiddleware =
                new HttpInFlightMiddleware(next => Task.CompletedTask, _factory.CreateGauge("in_flight", "help"));
            _countMiddleware =
                new HttpRequestCountMiddleware(next => Task.CompletedTask, _factory.CreateCounter("count", "help"));
            _durationMiddleware =
                new HttpRequestDurationMiddleware(next => Task.CompletedTask, _factory.CreateHistogram("duration", "help"));
        }

        [Benchmark]
        public void HttpInFlight()
        {
            Parallel.For(0, RequestCount, ParallelOptions,
                async _ => await _inFlightMiddleware.Invoke(new DefaultHttpContext()));
        }
        
        [Benchmark]
        public void HttpRequestCount()
        {
            Parallel.For(0, RequestCount, ParallelOptions,
                async _ => await _countMiddleware.Invoke(new DefaultHttpContext()));
        }
        
        [Benchmark]
        public void HttpRequestDuration()
        {
            Parallel.For(0, RequestCount, ParallelOptions,
                async _ => await _durationMiddleware.Invoke(new DefaultHttpContext()));
        }
    }
}