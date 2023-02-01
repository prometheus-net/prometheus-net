using System.Diagnostics;

namespace Prometheus;

/// <summary>
/// Collects basic .NET metrics about the current process. This is not meant to be an especially serious collector,
/// more of a producer of sample data so users of the library see something when they install it.
/// </summary>
public sealed class DotNetStats
{
    /// <summary>
    /// Registers the .NET metrics in the specified registry.
    /// </summary>
    public static void Register(CollectorRegistry registry)
    {
        var instance = new DotNetStats(Metrics.WithCustomRegistry(registry));
        registry.AddBeforeCollectCallback(instance.UpdateMetrics);
    }

    /// <summary>
    /// Registers the .NET metrics in the default metrics factory and registry.
    /// </summary>
    internal static void RegisterDefault()
    {
        var instance = new DotNetStats(Metrics.DefaultFactory);
        Metrics.DefaultRegistry.AddBeforeCollectCallback(instance.UpdateMetrics);
    }

    private readonly Process _process;
    private readonly List<Counter.Child> _collectionCounts = new List<Counter.Child>();
    private Gauge _totalMemory;
    private Gauge _virtualMemorySize;
    private Gauge _workingSet;
    private Gauge _privateMemorySize;
    private Counter _cpuTotal;
    private Gauge _openHandles;
    private Gauge _startTime;
    private Gauge _numThreads;

    private DotNetStats(IMetricFactory metricFactory)
    {
        _process = Process.GetCurrentProcess();

        var collectionCountsParent = metricFactory.CreateCounter("dotnet_collection_count_total", "GC collection count", new[] { "generation" });

        for (var gen = 0; gen <= GC.MaxGeneration; gen++)
        {
            _collectionCounts.Add(collectionCountsParent.Labels(gen.ToString()));
        }

        // Metrics that make sense to compare between all operating systems
        // Note that old versions of pushgateway errored out if different metrics had same name but different help string.
        // This is fixed in newer versions but keep the help text synchronized with the Go implementation just in case.
        // See https://github.com/prometheus/pushgateway/issues/194
        // and https://github.com/prometheus-net/prometheus-net/issues/89
        _startTime = metricFactory.CreateGauge("process_start_time_seconds", "Start time of the process since unix epoch in seconds.");
        _cpuTotal = metricFactory.CreateCounter("process_cpu_seconds_total", "Total user and system CPU time spent in seconds.");

        _virtualMemorySize = metricFactory.CreateGauge("process_virtual_memory_bytes", "Virtual memory size in bytes.");
        _workingSet = metricFactory.CreateGauge("process_working_set_bytes", "Process working set");
        _privateMemorySize = metricFactory.CreateGauge("process_private_memory_bytes", "Process private memory size");
        _openHandles = metricFactory.CreateGauge("process_open_handles", "Number of open handles");
        _numThreads = metricFactory.CreateGauge("process_num_threads", "Total number of threads");

        // .net specific metrics
        _totalMemory = metricFactory.CreateGauge("dotnet_total_memory_bytes", "Total known allocated memory");

        _startTime.SetToTimeUtc(_process.StartTime);
    }

    // The Process class is not thread-safe so let's synchronize the updates to avoid data tearing.
    private readonly object _updateLock = new object();

    private void UpdateMetrics()
    {
        try
        {
            lock (_updateLock)
            {
                _process.Refresh();

                for (var gen = 0; gen <= GC.MaxGeneration; gen++)
                    _collectionCounts[gen].IncTo(GC.CollectionCount(gen));

                _totalMemory.Set(GC.GetTotalMemory(false));
                _virtualMemorySize.Set(_process.VirtualMemorySize64);
                _workingSet.Set(_process.WorkingSet64);
                _privateMemorySize.Set(_process.PrivateMemorySize64);
                _cpuTotal.IncTo(_process.TotalProcessorTime.TotalSeconds);
                _openHandles.Set(_process.HandleCount);
                _numThreads.Set(_process.Threads.Count);
            }
        }
        catch (Exception)
        {
        }
    }
}