using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Prometheus;
using tester;

namespace Benchmark.NetCore;

// NB! This benchmark requires the Tester project to be running and the MetricPusherTester module to be active (to receive the data).
// If there is no tester listening, the results will be overly good because the runtime is under less I/O load.
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 0)]
public class MetricPusherBenchmarks
{
    private static string MetricPusherUrl = $"http://localhost:{TesterConstants.TesterPort}";

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Verify that there is a MetricPusher listening on Tester.

        using (var client = new HttpClient())
        {
            try
            {
                var result = await client.GetAsync(MetricPusherUrl);
                result.EnsureSuccessStatusCode();
            }
            catch
            {
                throw new Exception("You must start the Tester.NetCore project and configure it to use MetricPusherTester in its Program.cs before running this benchmark.");
            }
        }
    }


    [Benchmark]
    public async Task PushTest()
    {
        var registry = Metrics.NewCustomRegistry();
        var factory = Metrics.WithCustomRegistry(registry);

        var pusher = new MetricPusher(new MetricPusherOptions
        {
            Endpoint = MetricPusherUrl,
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