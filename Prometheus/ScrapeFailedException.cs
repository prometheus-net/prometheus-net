using System;

namespace Prometheus
{
    /// <summary>
    /// Signals to the metrics server that metrics are currently unavailable. Thrown from "before collect" callbacks.
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
