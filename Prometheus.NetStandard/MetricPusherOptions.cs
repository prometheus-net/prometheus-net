using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Prometheus
{
    public sealed class MetricPusherOptions
    {
        internal static readonly MetricPusherOptions Default = new MetricPusherOptions();

        public string? Endpoint { get; set; }
        public string? Job { get; set; }
        public string? Instance { get; set; }
        public long IntervalMilliseconds { get; set; } = 1000;
        public IEnumerable<Tuple<string, string>>? AdditionalLabels { get; set; }
        public CollectorRegistry? Registry { get; set; }

        /// <summary>
        /// If null, a singleton HttpClient will be used.
        /// </summary>
        public Func<HttpClient>? HttpClientProvider { get; set; }
    }
}
