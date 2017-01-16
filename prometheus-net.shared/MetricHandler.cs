using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using Prometheus.Advanced;

namespace Prometheus
{
    public abstract class MetricHandler : IMetricServer
    {
        protected readonly ICollectorRegistry _registry;
        private IDisposable _schedulerDelegate;

        protected MetricHandler(IEnumerable<IOnDemandCollector> standardCollectors = null,
            ICollectorRegistry registry = null)
        {
            _registry = registry ?? DefaultCollectorRegistry.Instance;
            if (_registry == DefaultCollectorRegistry.Instance)
            {
                // Default to DotNetStatsCollector if none specified
                // For no collectors, pass an empty collection
                if (standardCollectors == null)
                    standardCollectors = new[] { new DotNetStatsCollector() };

                DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(standardCollectors);
            }
        }

        public void Start(IScheduler scheduler = null)
        {
            _schedulerDelegate = StartLoop(scheduler ?? Scheduler.Default);
        }

        public void Stop()
        {
            if (_schedulerDelegate != null) _schedulerDelegate.Dispose();
            StopInner();
        }

        protected virtual void StopInner()
        {
            
        }

        protected abstract IDisposable StartLoop(IScheduler scheduler);
    }
}
