using System;
using System.Collections.Generic;
using System.Text;

namespace Prometheus
{
    public sealed class GrpcRequestDurationOptions: GrpcMetricsOptionsBase
    {
        public ICollector<IHistogram>? Histogram { get; set; }
    }
}
