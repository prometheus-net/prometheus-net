using System;
using System.Collections.Generic;

namespace Prometheus
{
    public sealed class SummaryConfiguration : MetricConfiguration
    {
        internal static readonly SummaryConfiguration Default = new SummaryConfiguration();

        public IReadOnlyList<QuantileEpsilonPair> Objectives { get; set; } = Summary.DefObjectivesArray;
        public TimeSpan MaxAge { get; set; } = Summary.DefMaxAge;
        public int AgeBuckets { get; set; } = Summary.DefAgeBuckets;
        public int BufferSize { get; set; } = Summary.DefBufCap;
    }
}
