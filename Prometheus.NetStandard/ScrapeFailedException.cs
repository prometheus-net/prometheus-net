using System;

namespace Prometheus
{
    /// <summary>
    /// Signals to the metrics server that metrics from on-demand collectors are currently unavailable.
    /// This causes the entire export operation to fail - even if some metrics are available, they will not be exported.
    /// 
    /// The exception message will be delivered as the HTTP response body by the exporter.
    /// </summary>
    [Serializable]
    public class ScrapeFailedException : Exception
    {
        public ScrapeFailedException() { }
        public ScrapeFailedException(string message) : base(message) { }
        public ScrapeFailedException(string message, Exception inner) : base(message, inner) { }
        protected ScrapeFailedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
