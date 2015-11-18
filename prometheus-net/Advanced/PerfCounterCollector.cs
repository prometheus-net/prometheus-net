using System;
using System.Collections.Generic;
using System.Diagnostics;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    /// <summary>
    /// Collects metrics on standard Performance Counters
    /// </summary>
    public class PerfCounterCollector : ICollector
    {
        private const string MemCat = ".NET CLR Memory";
        private const string ProcCat = "Process";
        
        private static readonly string[] StandardPerfCounters =
        {
            MemCat, "Gen 0 heap size",
            MemCat, "Gen 1 heap size",
            MemCat, "Gen 2 heap size",
            MemCat, "Large Object Heap size",
            MemCat, "% Time in GC",
            ProcCat, "% Processor Time",
            ProcCat, "Private Bytes",
            ProcCat, "Working Set",
            ProcCat, "Virtual Bytes",
        };

        readonly List<Tuple<Gauge, PerformanceCounter>> _collectors = new List<Tuple<Gauge, PerformanceCounter>>();
        private readonly string _instanceName;
        private Counter _perfErrors;

        private static bool IsLinux()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    return true;

                default:
                    return false;
            }
        }

        public PerfCounterCollector()
        {
            Name = "performanceCounters";
            Process currentProcess = Process.GetCurrentProcess();
            _instanceName = currentProcess.ProcessName;
            if (IsLinux())
            {
                //on mono/linux instance name should be pid
                _instanceName = currentProcess.Id.ToString();
            }
        }

        public void RegisterStandardPerfCounters()
        {
            for (int i = 0; i < StandardPerfCounters.Length; i += 2)
            {
                var category = StandardPerfCounters[i];
                var name = StandardPerfCounters[i + 1];

                RegisterPerfCounter(category, name);
            }

            _perfErrors = Metrics.CreateCounter("performance_counter_errors_total",
                "Total number of errors that occured during performance counter collections");
        }

        public void RegisterPerfCounter(string category, string name)
        {
            //Gauge.Child labelledMetric = Metrics.CreateGauge(GetName(category, name), GetHelp(name), "process").Labels(_processName);
            Gauge gauge = Metrics.CreateGauge(GetName(category, name), GetHelp(name));
            _collectors.Add(Tuple.Create(gauge, new PerformanceCounter(category, name, _instanceName)));
        }

        private string GetHelp(string name)
        {
            return name + " Perf Counter";
        }

        private string GetName(string category, string name)
        {
            return ToPromName(category) + "_" + ToPromName(name);
        }

        private string ToPromName(string name)
        {
            return name.Replace("%", "pct").Replace(" ", "_").Replace(".", "dot").ToLowerInvariant();
        }

        public MetricFamily Collect()
        {
            //update existing counters during a collect call
            foreach (var collector in _collectors)
            {
                try
                {
                    collector.Item1.Set(collector.Item2.NextValue());
                }
                catch (Exception)
                {
                    _perfErrors.Inc();
                }
            }

            //the way this collector is registered on DefaultCollectorRegistry ensures that this is the very first Collector collected
            //so by the time the collectors registered in the _collectors list are collected they will all be up-to-date
            //this works but a bit hacky...

            //don't return anything - this will be dropped by the registry...
            return null;
        }

        public string Name { get; private set; }
        public string[] LabelNames { get {return new string[0];} }
    }
}