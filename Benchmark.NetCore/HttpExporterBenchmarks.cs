using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Prometheus;
using Prometheus.HttpMetrics;
using System.Threading.Tasks;

namespace Benchmark.NetCore
{
    [MemoryDiagnoser]
    public class HttpExporterBenchmarks
    {
        private CollectorRegistry _registry;
        private MetricFactory _factory;
        private HttpInFlightMiddleware _inFlightMiddleware;
        private HttpRequestCountMiddleware _countMiddleware;
        private HttpRequestDurationMiddleware _durationMiddleware;

        private ParallelOptions ParallelOptions => new ParallelOptions
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
            _registry = Metrics.NewCustomRegistry();
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