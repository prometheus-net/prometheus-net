using System;
using System.Reactive.Linq;
using Prometheus;

namespace tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var metricServer = new MetricServer(1234);
            metricServer.Start();

            var counter = Metrics.CreateCounter("test4", "helpcounter", "labelCounter");
            var gauge = Metrics.CreateGauge("test3", "helpgauge", "testLabel");
            var hist = Metrics.CreateHistogram("test_hist", "helpbucket", buckets: new[] { 0, 0.2, 0.4, 0.6, 0.8, 0.9 });//.WithLabel("testlabel", "2");
            var summary = Metrics.CreateSummary("test_summary", "help3", "smm");

            var random = new Random();
            Observable.Interval(TimeSpan.FromSeconds(0.5)).Subscribe(l =>
            {
                counter.Inc();
                counter.Labels("test").Inc(2);
                gauge.Observe(random.NextDouble() + 2);
                hist.Observe(random.NextDouble());
                summary.Observe(random.NextDouble());
            });


            Console.WriteLine("ENTER to quit");
            Console.ReadLine();
            metricServer.Stop();
        }
    }
}
