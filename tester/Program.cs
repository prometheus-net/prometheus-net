using System;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using Prometheus;

namespace tester
{
    class Program
    {
        static void Main(string[] args)
        {
            // use MetricServerTester or MetricPusherTester to select between metric handlers
            var tester = new MetricServerTester();
            tester.OnStart();

            var metricServer = tester.InitializeMetricHandler();
            metricServer.Start();

            var counter = Metrics.CreateCounter("myCounter", "help text", labelNames: new []{ "method", "endpoint"});
            counter.Labels("GET", "/").Inc();
            counter.Labels("POST", "/cancel").Inc();


            var gauge = Metrics.CreateGauge("gauge", "help text");
            gauge.Inc(3.4);
            gauge.Dec(2.1);
            gauge.Set(5.3);
            gauge.SetTimestamp(DateTimeOffset.Now.AddDays(-1));

            var hist = Metrics.CreateHistogram("myHistogram", "help text", buckets: new[] { 0, 0.2, 0.4, 0.6, 0.8, 0.9 });
            hist.Observe(0.4);

            var summary = Metrics.CreateSummary("mySummary", "help text");
            summary.Observe(5.3);

            var random = new Random();
            Observable.Interval(TimeSpan.FromSeconds(0.5)).Subscribe(l =>
            {
                counter.Inc();
                counter.Labels("GET", "/").Inc(2);
                gauge.Set(random.NextDouble() + 2);
                gauge.SetTimestamp(DateTimeOffset.Now.AddDays(-1));
                hist.Observe(random.NextDouble());
                summary.Observe(random.NextDouble());

                tester.OnObservation();
            });

            Console.WriteLine("Press enter to stop metricServer");
            Console.ReadLine();
            metricServer.Stop();

            tester.OnEnd();

            Console.WriteLine("Press enter to stop tester");
            Console.ReadLine();
        }
    }
}
