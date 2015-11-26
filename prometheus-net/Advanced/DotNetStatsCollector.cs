using System;
using System.Collections.Generic;
using System.Diagnostics;

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

            _virtualMemorySize = Metrics.CreateGauge("process_virtual_bytes", "Process virtual memory size");
            _workingSet = Metrics.CreateGauge("process_working_set", "Process working set");
            _privateMemorySize = Metrics.CreateGauge("process_private_bytes", "Process private memory size");
            _totalMemory = Metrics.CreateGauge("dotnet_totalmemory", "Total known allocated memory");
            _perfErrors = Metrics.CreateCounter("dotnet_collection_errors_total", "Total number of errors that occured during collections");
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
            }
            catch (Exception)
            {
                _perfErrors.Inc();
            }
        }
    }
}