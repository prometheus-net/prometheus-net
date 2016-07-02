using System;
using System.Collections.Generic;
using System.Diagnostics;
using Prometheus;

namespace Prometheus.Advanced
{
    /// <summary>
    /// Collects metrics on .net without performance counters
    /// </summary>
    public class DotNetStatsCollector : IOnDemandCollector
    {
        private readonly Process _process;
        private Counter _perfErrors;
        private readonly List<Counter.Child> _collectionCounts = new List<Counter.Child>();
        private Gauge _totalMemory;
        private Gauge _virtualMemorySize;
        private Gauge _workingSet;
        private Gauge _privateMemorySize;
        private Counter _cpuTotal;
        private Gauge _openHandles;
        private Gauge _startTime;
        private Gauge _numThreads;

        public DotNetStatsCollector()
        {
            _process = Process.GetCurrentProcess();
        }
        
        public void RegisterMetrics()
        {
            var collectionCountsParent = Metrics.CreateCounter("dotnet_collection_count_total", "GC collection count", new []{"generation"});
            
            for (var gen = 0; gen <= GC.MaxGeneration; gen++)
            {
                _collectionCounts.Add(collectionCountsParent.Labels(gen.ToString()));
            }

            // Metrics that make sense to compare between all operating systems
            _startTime = Metrics.CreateGauge("process_start_time_seconds", "Start time of the process since unix epoch in seconds");
            _cpuTotal = Metrics.CreateCounter("process_cpu_seconds_total", "Total user and system CPU time spent in seconds");

            // Windows specific metrics
            _virtualMemorySize = Metrics.CreateGauge("process_windows_virtual_bytes", "Process virtual memory size");
            _workingSet = Metrics.CreateGauge("process_windows_working_set", "Process working set");
            _privateMemorySize = Metrics.CreateGauge("process_windows_private_bytes", "Process private memory size");
            _openHandles = Metrics.CreateGauge("process_windows_open_handles", "Number of open handles");
            _numThreads = Metrics.CreateGauge("process_windows_num_threads", "Total number of threads");

            // .net specific metrics
            _totalMemory = Metrics.CreateGauge("dotnet_totalmemory", "Total known allocated memory");
            _perfErrors = Metrics.CreateCounter("dotnet_collection_errors_total", "Total number of errors that occured during collections");
            
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _startTime.Set((_process.StartTime.ToUniversalTime() - epoch).TotalSeconds);
        }

        public void UpdateMetrics()
        {
            try
            {
                for (var gen = 0; gen <= GC.MaxGeneration; gen++)
                {
                    var collectionCount = _collectionCounts[gen];
                    collectionCount.Inc(GC.CollectionCount(gen) - collectionCount.Value);
                }

                _totalMemory.Set(GC.GetTotalMemory(false));
                _virtualMemorySize.Set(_process.VirtualMemorySize64);
                _workingSet.Set(_process.WorkingSet64);
                _privateMemorySize.Set(_process.PrivateMemorySize64);
                _cpuTotal.Inc(_process.TotalProcessorTime.TotalSeconds - _cpuTotal.Value);
                //_openHandles.Set(_process.HandleCount);
                _numThreads.Set(_process.Threads.Count);
            }
            catch (Exception)
            {
                _perfErrors.Inc();
            }
        }
    }
}