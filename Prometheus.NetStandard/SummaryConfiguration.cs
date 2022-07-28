using System;
using System.Collections.Generic;

namespace Prometheus
{
    public sealed class SummaryConfiguration : MetricConfiguration
    {
        internal static readonly SummaryConfiguration Default = new SummaryConfiguration();

        /// <summary>
        /// Pairs of quantiles and allowed error values (epsilon).
        /// 
        /// For example, a quantile of 0.95 with an epsilon of 0.01 means the calculated value
        /// will be between the 94th and 96th quantile.
        /// 
        /// If null, no quantiles will be calculated!
        /// </summary>
        public IReadOnlyList<QuantileEpsilonPair> Objectives { get; set; } = Summary.DefObjectivesArray;

        /// <summary>
        /// Time span over which to calculate the summary.
        /// </summary>
        public TimeSpan MaxAge { get; set; } = Summary.DefMaxAge;

        /// <summary>
        /// Number of buckets used to control measurement expiration.
        /// </summary>
        public int AgeBuckets { get; set; } = Summary.DefAgeBuckets;

        /// <summary>
        /// Buffer size limit. Use multiples of 500 to avoid waste, as internal buffers use that size.
        /// </summary>
        public int BufferSize { get; set; } = Summary.DefBufCap;
    }
}
