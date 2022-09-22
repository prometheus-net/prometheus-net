using System;
using System.Diagnostics;

namespace Prometheus
{
    public sealed class DiagnosticSourceAdapterOptions
    {
        internal static readonly DiagnosticSourceAdapterOptions Default = new DiagnosticSourceAdapterOptions();

        /// <summary>
        /// By default we subscribe to all listeners but this allows you to filter by listener.
        /// </summary>
        public Func<DiagnosticListener, bool> ListenerFilterPredicate = _ => true;

        public CollectorRegistry Registry = Metrics.DefaultRegistry;
    }
}
