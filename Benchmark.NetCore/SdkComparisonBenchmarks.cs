using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// We compare pure measurement (not serializing the data) with prometheus-net SDK and OpenTelemetry .NET SDK.
/// </summary>
/// <remarks>
/// Design logic:
/// * Metrics are initialized once on application startup.
/// * Metrics typically measure "sessions" - there are sets of metrics that are related through shared identifiers and a shared lifetime (e.g. HTTP request),
///   with all the identifiers for the metrics created when the sesison is initialized (e.g. when the HTTP connection is established).
/// 
/// Excluded from measurement:
/// * Meter setup (because meters are created once on application setup and not impactful later).
/// * Test data generation (session numbers and identifier strings) as it is SDK-neutral.
/// 
/// We have a separate benchmark to compare the setup stage (just re-runs the setup logic in measured phase).
/// 
/// We also do not benchmark "observable" metrics that are only polled at time of collection.
/// Both SDKs support it as an optimization (though OpenTelemetry forces it for counters) but let's try keep the logic here simple and exclude it for now.
/// </remarks>
[MemoryDiagnoser]
public class SdkComparisonBenchmarks
{
    private const int CounterCount = 100;
    private const int HistogramCount = 100;

    // Unique sets of label/tag values per metric. You can think of each one as a "session" we are reporting data for.
    private const int TimeseriesPerMetric = 100;

    private static readonly string[] LabelNames = new[] { "environment", "server", "session_id" };
    private const string Label1Value = "production";
    private const string Label2Value = "hkhk298599-qps010-n200";

    // How many observations we take during a single benchmark invocation (for each timeseries).
    private const int ObservationCount = 1000;

    private static readonly string[] SessionIds = new string[TimeseriesPerMetric];

    static SdkComparisonBenchmarks()
    {
        for (var i = 0; i < SessionIds.Length; i++)
            SessionIds[i] = Guid.NewGuid().ToString();
    }

    [Params(MetricsSdk.PrometheusNet, MetricsSdk.OpenTelemetry)]
    public MetricsSdk Sdk { get; set; }

    public enum MetricsSdk
    {
        PrometheusNet,
        OpenTelemetry
    }

    /// <summary>
    /// Contains all the context that gets initialized at iteration setup time.
    /// 
    /// This data set is:
    /// 1) Not included in the performance measurements.
    /// 2) Reused for each invocation that is part of the same iteration.
    /// </summary>
    private abstract class MetricsContext : IDisposable
    {
        /// <summary>
        /// Records an observation with all the counter-type metrics for each session.
        /// </summary>
        public abstract void ObserveCounter(double value);

        /// <summary>
        /// Records an observation with all the histogram-type metrics for each session.
        /// </summary>
        public abstract void ObserveHistogram(double value);

        public virtual void Dispose() { }
    }

    private sealed class PrometheusNetMetricsContext : MetricsContext
    {
        private readonly List<Prometheus.Counter.Child> _counterInstances = new(CounterCount * TimeseriesPerMetric);
        private readonly List<Histogram.Child> _histogramInstances = new(HistogramCount * TimeseriesPerMetric);

        public PrometheusNetMetricsContext()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            // Do not emit any exemplars in this benchmark, as they are not yet equally supported by the SDKs.
            factory.ExemplarBehavior = ExemplarBehavior.NoExemplars();

            for (var counterIndex = 0; counterIndex < CounterCount; counterIndex++)
            {
                var counter = factory.CreateCounter("counter_" + counterIndex, "", LabelNames);

                for (var i = 0; i < TimeseriesPerMetric; i++)
                    _counterInstances.Add(counter.WithLabels(Label1Value, Label2Value, SessionIds[i]));
            }

            for (var histogramIndex = 0; histogramIndex < HistogramCount; histogramIndex++)
            {
                var histogram = factory.CreateHistogram("histogram_" + histogramIndex, "", LabelNames);

                for (var i = 0; i < TimeseriesPerMetric; i++)
                    _histogramInstances.Add(histogram.WithLabels(Label1Value, Label2Value, SessionIds[i]));
            }
        }

        public override void ObserveCounter(double value)
        {
            foreach (var counter in _counterInstances)
                counter.Inc(value);
        }

        public override void ObserveHistogram(double value)
        {
            foreach (var histogram in _histogramInstances)
                histogram.Observe(value);
        }
    }

    private sealed class OpenTelemetryMetricsContext : MetricsContext
    {
        private const string MeterBaseName = "benchmark";

        private readonly Meter _meter;
        private readonly MeterProvider _provider;

        private readonly List<Counter<double>> _counters = new(CounterCount);
        private readonly List<Histogram<double>> _histograms = new(HistogramCount);

        private readonly List<TagList> _sessions = new(TimeseriesPerMetric);

        public OpenTelemetryMetricsContext()
        {
            // We use a randomized name every time because otherwise there appears to be some "shared state" between benchmark invocations,
            // at least for the "setup" benchmark which keeps getting slower every time we call it with the same metric name.
            _meter = new Meter(MeterBaseName + Guid.NewGuid());

            _provider = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .AddPrometheusExporter()
                .AddMeter(_meter.Name)
                .Build();

            for (var i = 0; i < CounterCount; i++)
                _counters.Add(_meter.CreateCounter<double>("counter_" + i));

            for (var i = 0; i < HistogramCount; i++)
                _histograms.Add(_meter.CreateHistogram<double>("histogram_" + i));

            for (var i = 0; i < TimeseriesPerMetric; i++)
            {
                var tag1 = new KeyValuePair<string, object>(LabelNames[0], Label1Value);
                var tag2 = new KeyValuePair<string, object>(LabelNames[1], Label2Value);
                var tag3 = new KeyValuePair<string, object>(LabelNames[2], SessionIds[i]);

                var tagList = new TagList(new[] { tag1, tag2, tag3 });
                _sessions.Add(tagList);
            }
        }

        public override void ObserveCounter(double value)
        {
            foreach (var session in _sessions)
            {
                foreach (var counter in _counters)
                    counter.Add(value, session);
            }
        }

        public override void ObserveHistogram(double value)
        {
            foreach (var session in _sessions)
            {
                foreach (var histogram in _histograms)
                    histogram.Record(value, session);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _provider.Dispose();
        }
    }

    private MetricsContext _context;

    [IterationSetup]
    public void Setup()
    {
        _context = Sdk switch
        {
            MetricsSdk.PrometheusNet => new PrometheusNetMetricsContext(),
            MetricsSdk.OpenTelemetry => new OpenTelemetryMetricsContext(),
            _ => throw new NotImplementedException(),
        };
    }

    [Benchmark]
    public void CounterMeasurements()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveCounter(observation);
    }

    [Benchmark]
    public void HistogramMeasurements()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveHistogram(observation);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [Benchmark]
    public void SetupBenchmark()
    {
        // Here we just do the setup again, but this time as part of the measured data set, to compare the setup cost between SDKs.

        // We need to dispose of the automatically created context, in case there are any SDK-level singleton resources (which we do not want to accidentally reuse).
        _context.Dispose();

        Setup();
    }
}
