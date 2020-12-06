using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Prometheus;
using System.Threading.Tasks;
using Grpc.AspNetCore.Server;
using Grpc.Core;

namespace Benchmark.NetCore
{
    [MemoryDiagnoser]
    public class GrpcExporterBenchmarks
    {
        private CollectorRegistry _registry;
        private MetricFactory _factory;
        private GrpcRequestCountMiddleware _countMiddleware;
        private GrpcRequestDurationMiddleware _durationMiddleware;
        private DefaultHttpContext _ctx;

        [Params(1000, 10000)]
        public int RequestCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _ctx = new DefaultHttpContext();
            _ctx.SetEndpoint(new Endpoint(
                ctx => Task.CompletedTask, 
                new EndpointMetadataCollection(new GrpcMethodMetadata(typeof(int),
                    new Method<object, object>(MethodType.Unary,
                        "test",
                        "test",
                        new Marshaller<object>(o => new byte[0], c => null), 
                        new Marshaller<object>(o => new byte[0], c => null)))),
                "test"));
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);

            _countMiddleware = new GrpcRequestCountMiddleware(next => Task.CompletedTask, new GrpcRequestCountOptions
            {
                Counter = _factory.CreateCounter("count", "help")
            });
            _durationMiddleware = new GrpcRequestDurationMiddleware(next => Task.CompletedTask, new GrpcRequestDurationOptions
            {
                Histogram = _factory.CreateHistogram("duration", "help")
            });
        }

        [Benchmark]
        public async Task GrpcRequestCount()
        {
            for (var i = 0; i < RequestCount; i++)
                await _countMiddleware.Invoke(_ctx);
        }

        [Benchmark]
        public async Task GrpcRequestDuration()
        {
            for (var i = 0; i < RequestCount; i++)
                await _durationMiddleware.Invoke(_ctx);
        }
    }
}