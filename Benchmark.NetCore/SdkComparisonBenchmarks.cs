using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;
using Prometheus;

namespace Benchmark.NetCore;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100-rc.2.23502.2
  [Host]     : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2
  Job-AGCLMW : .NET 7.0.13 (7.0.1323.51816), X64 RyuJIT AVX2


| Method                        | Job        | MaxIterationCount | Mean        | Error       | StdDev      | Gen0     | Gen1     | Allocated |
|------------------------------ |----------- |------------------ |------------:|------------:|------------:|---------:|---------:|----------:|
| PromNetCounter                | DefaultJob | Default           |    771.5 us |     5.54 us |     4.91 us |        - |        - |       1 B |
| PromNetHistogram              | DefaultJob | Default           |  2,747.5 us |    27.86 us |    26.06 us |        - |        - |       3 B |
| OTelCounter                   | DefaultJob | Default           | 14,470.8 us |    54.28 us |    48.12 us |        - |        - |      12 B |
| OTelHistogram                 | DefaultJob | Default           | 15,856.9 us |   193.51 us |   181.01 us |        - |        - |      25 B |
| PromNetHistogramForAdHocLabel | Job-AGCLMW | 16                |  8,804.0 us | 1,083.49 us | 1,013.49 us | 500.0000 | 234.3750 | 3184062 B |
| OTelHistogramForAdHocLabel    | Job-AGCLMW | 16                |    580.8 us |     6.08 us |     5.69 us |  14.6484 |        - |   96001 B |
*/

/// <summary>
/// We compare pure measurement (not serializing the data) with prometheus-net SDK and OpenTelemetry .NET SDK.
/// </summary>
/// <remarks>
/// Design logic:
/// * Metrics are initialized once on application startup.
/// * Metrics typically measure "sessions" - there are sets of metrics that are related through shared identifiers and a shared lifetime (e.g. HTTP request),
///   with all the identifiers for the metrics created when the sesison is initialized (e.g. when the HTTP connection is established).
/// * Metrics typically are also used report SLI (Service Level Indicator); these involve emitting a lot of unique dimension values, for example: CustomerId.
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

        /// <summary>
        /// Records an observation with one random label value as ad-hoc using a Histogram.
        /// </summary>
        public abstract void ObserveHistogramWithAnAdHocLabelValue(double value);

        public virtual void Dispose() { }
    }

    private sealed class PrometheusNetMetricsContext : MetricsContext
    {
        private readonly List<Counter.Child> _counterInstances = new(TimeseriesPerMetric);
        private readonly List<Histogram.Child> _histogramInstances = new(TimeseriesPerMetric);
        private readonly Histogram _histogramForAdHocLabels;

        private readonly KestrelMetricServer _server;

        public PrometheusNetMetricsContext()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            // Do not emit any exemplars in this benchmark, as they are not yet equally supported by the SDKs.
            factory.ExemplarBehavior = ExemplarBehavior.NoExemplars();

            var counter = factory.CreateCounter("counter", "", LabelNames);

            for (var i = 0; i < TimeseriesPerMetric; i++)
                _counterInstances.Add(counter.WithLabels(Label1Value, Label2Value, SessionIds[i]));

            var histogram = factory.CreateHistogram("histogram", "", LabelNames);

            _histogramForAdHocLabels = factory.CreateHistogram("histogramForAdHocLabels", "", LabelNames);

            for (var i = 0; i < TimeseriesPerMetric; i++)
                _histogramInstances.Add(histogram.WithLabels(Label1Value, Label2Value, SessionIds[i]));

            // `AddPrometheusHttpListener` of OpenTelemetry creates an HttpListener.
            // Start a listener/server for Prometheus benchmarks for a fair comparison.
            // We listen on 127.0.0.1:<random free port> to avoid firewall prompts (we do not expect to receive any traffic).
            _server = new KestrelMetricServer("127.0.0.1", port: 0);
            _server.Start();
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

        public override void ObserveHistogramWithAnAdHocLabelValue(double value)
        {
            _histogramForAdHocLabels.WithLabels(Label1Value, Label2Value, Guid.NewGuid().ToString()).Observe(value);
        }

        public override void Dispose()
        {
            base.Dispose();

            _server.Dispose();
        }
    }

    private sealed class OpenTelemetryMetricsContext : MetricsContext
    {
        private const string MeterBaseName = "benchmark";

        private readonly Meter _meter;
        private readonly MeterProvider _provider;

        private readonly Counter<double> _counter;
        private readonly Histogram<double> _histogram;
        private readonly Histogram<double> _histogramForAdHocLabels;

        public OpenTelemetryMetricsContext()
        {
            // We use a randomized name every time because otherwise there appears to be some "shared state" between benchmark invocations,
            // at least for the "setup" benchmark which keeps getting slower every time we call it with the same metric name.
            _meter = new Meter(MeterBaseName + Guid.NewGuid());

            _counter = _meter.CreateCounter<double>("counter");

            _histogram = _meter.CreateHistogram<double>("histogram");

            _histogramForAdHocLabels = _meter.CreateHistogram<double>("histogramForAdHocLabels");

            _provider = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .AddView("histogram", new OpenTelemetry.Metrics.HistogramConfiguration() { RecordMinMax = false})
                .AddMeter(_meter.Name)
                .AddPrometheusHttpListener()
                .Build();
        }

        public override void ObserveCounter(double value)
        {
            for (int i = 0; i < SessionIds.Length; i++)
            {
                var tag1 = new KeyValuePair<string, object>(LabelNames[0], Label1Value);
                var tag2 = new KeyValuePair<string, object>(LabelNames[1], Label2Value);
                var tag3 = new KeyValuePair<string, object>(LabelNames[2], SessionIds[i]);
                _counter.Add(value, tag1, tag2, tag3);
            }
        }

        public override void ObserveHistogram(double value)
        {
            for (int i = 0; i < SessionIds.Length; i++)
            {
                var tag1 = new KeyValuePair<string, object>(LabelNames[0], Label1Value);
                var tag2 = new KeyValuePair<string, object>(LabelNames[1], Label2Value);
                var tag3 = new KeyValuePair<string, object>(LabelNames[2], SessionIds[i]);
                _histogram.Record(value, tag1, tag2, tag3);
            }
        }

        public override void ObserveHistogramWithAnAdHocLabelValue(double value)
        {
            var tag1 = new KeyValuePair<string, object>(LabelNames[0], Label1Value);
            var tag2 = new KeyValuePair<string, object>(LabelNames[1], Label2Value);
            var tag3 = new KeyValuePair<string, object>(LabelNames[2], Guid.NewGuid().ToString());
            _histogramForAdHocLabels.Record(value, tag1, tag2, tag3);
        }

        public override void Dispose()
        {
            base.Dispose();

            _provider.Dispose();
        }
    }

    private MetricsContext _context;

    [GlobalSetup(Targets = new string[] {nameof(OTelCounter), nameof(OTelHistogram), nameof(OTelHistogramForAdHocLabel)})]
    public void OpenTelemetrySetup()
    {
        _context = new OpenTelemetryMetricsContext();
    }

    [GlobalSetup(Targets = new string[] { nameof(PromNetCounter), nameof(PromNetHistogram), nameof(PromNetHistogramForAdHocLabel) })]
    public void PrometheusNetSetup()
    {
        _context = new PrometheusNetMetricsContext();
    }

    [Benchmark]
    public void PromNetCounter()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveCounter(observation);
    }

    [Benchmark]
    public void PromNetHistogram()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveHistogram(observation);
    }

    [Benchmark]
    [MaxIterationCount(16)] // Need to set a lower iteration count as this benchmarks allocates a lot memory and takes too long to complete with the default number of iterations.
    public void PromNetHistogramForAdHocLabel()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveHistogramWithAnAdHocLabelValue(observation);
    }

    [Benchmark]
    public void OTelCounter()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveCounter(observation);
    }

    [Benchmark]
    public void OTelHistogram()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveHistogram(observation);
    }

    [Benchmark]
    [MaxIterationCount(16)] // Set the same number of iteration count as the corresponding PromNet benchmark.
    public void OTelHistogramForAdHocLabel()
    {
        for (var observation = 0; observation < ObservationCount; observation++)
            _context.ObserveHistogramWithAnAdHocLabelValue(observation);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }
}
