using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;

namespace Prometheus
{
    public interface IMetricServer
    {
        void Start(IScheduler scheduler = null);
        void Stop();
    }
}
