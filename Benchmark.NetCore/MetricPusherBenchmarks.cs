using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Prometheus;

namespace Benchmark.NetCore
{
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, warmupCount:0)]
    public class MetricPusherBenchmarks
    {
        [Benchmark]
        public async Task PushTest()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);
            
            var pusher = new MetricPusher(new MetricPusherOptions
            {
                Endpoint = "http://127.0.0.1:9091/metrics",
                Registry = registry,
                IntervalMilliseconds = 30,
                Job = "job"
            });
            pusher.Start();

            var counters = new List<Counter>();
            for (int i = 0; i < 1000; i++)
            {
                var counter = factory.CreateCounter($"Counter{i}", String.Empty);
                counters.Add(counter);
            }

            var ct = new CancellationTokenSource();
            var incTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    foreach (var counter in counters)
                    {
                        counter.Inc();
                    }

                    await Task.Delay(30);
                }
            });

            await Task.Delay(5000);
            ct.Cancel();
            await incTask;

            pusher.Stop();
        }
    }
}