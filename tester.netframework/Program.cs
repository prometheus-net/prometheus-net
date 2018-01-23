using Prometheus;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace tester
{
    class Program
    {
        static void Main(string[] args)
        {
            // use MetricServerTester or MetricPusherTester to select between metric handlers
            var tester = new MetricPusherTester();
            tester.OnStart();

            var metricServer = tester.InitializeMetricHandler();
            metricServer.Start();

            var counter = Metrics.CreateCounter("myCounter", "help text", labelNames: new[] { "method", "endpoint" });
            counter.Labels("GET", "/").Inc();
            counter.Labels("POST", "/cancel").Inc();


            var gauge = Metrics.CreateGauge("gauge", "help text");
            gauge.Inc(3.4);
            gauge.Dec(2.1);
            gauge.Set(5.3);

            var hist = Metrics.CreateHistogram("myHistogram", "help text", buckets: new[] { 0, 0.2, 0.4, 0.6, 0.8, 0.9 });
            hist.Observe(0.4);

            var summary = Metrics.CreateSummary("mySummary", "help text");
            summary.Observe(5.3);

            var cts = new CancellationTokenSource();

            var random = new Random();

            // Update metrics on a regular interval until told to stop.
            var updateInterval = TimeSpan.FromSeconds(0.5);
            var updateTask = Task.Factory.StartNew(async delegate
            {
                while (!cts.IsCancellationRequested)
                {
                    var duration = Stopwatch.StartNew();

                    counter.Inc();
                    counter.Labels("GET", "/").Inc(2);
                    gauge.Set(random.NextDouble() + 2);
                    hist.Observe(random.NextDouble());
                    summary.Observe(random.NextDouble());

                    tester.OnObservation();

                    var sleepTime = updateInterval - duration.Elapsed;

                    if (sleepTime > TimeSpan.Zero)
                        await Task.Delay(sleepTime, cts.Token);
                }
            }).Result;

            Console.WriteLine("Press enter to stop metricServer");
            Console.ReadLine();

            cts.Cancel();
            try
            {
                updateTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }

            metricServer.StopAsync().GetAwaiter().GetResult();

            tester.OnEnd();

            Console.WriteLine("Press enter to stop tester");
            Console.ReadLine();
        }
    }
}
