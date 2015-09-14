using System;
using System.Diagnostics;
using System.Net;
using System.Reactive.Concurrency;
using Prometheus.Advanced;
using Prometheus.Internal;

namespace Prometheus
{
    public class MetricServer
    {
        private const string PROTO_HEADER = "application/vnd.google.protobuf; proto=io.prometheus.client.MetricFamily; encoding=delimited";
        private readonly HttpListener _httpListener = new HttpListener();
        private static readonly string ProtoHeaderNoSpace = PROTO_HEADER.Replace(" ", "");
        private readonly ICollectorRegistry _registry;

        public MetricServer(int port, string url = "metrics/", ICollectorRegistry registry = null) : this("+", port, url, registry)
        {
        }

        public MetricServer(string hostname, int port, string url = "metrics/", ICollectorRegistry registry = null)
        {
            _registry = registry ?? DefaultCollectorRegistry.Instance;
            _httpListener.Prefixes.Add(string.Format("http://{0}:{1}/{2}", hostname, port, url));
            if (_registry == DefaultCollectorRegistry.Instance)
            {
                DefaultCollectorRegistry.Instance.RegisterStandardPerfCounters();
            }
        }

        public void Start(IScheduler scheduler = null)
        {
            _httpListener.Start();

            StartLoop(scheduler ?? Scheduler.Default);
        }

        private void ProcessScrapeRequest(HttpListenerContext context)
        {
            var response = context.Response;
            response.StatusCode = 200;

            const string text = "text/plain; version=0.0.4";

            string type = PROTO_HEADER;
            
            if (!context.Request.Headers.Get("Accept").Replace(" ", "").Contains(ProtoHeaderNoSpace))
            {
                type = text;
            }

            response.AddHeader("Content-Type", type);

            var collected = _registry.CollectAll();
            using (var outputStream = response.OutputStream)
            {
                if (type == text)
                {
                    AsciiFormatter.Format(outputStream, collected);
                }
                else
                {
                    ProtoFormatter.Format(outputStream, collected);
                }
            }
            response.Close();
        }

        private void StartLoop(IScheduler scheduler)
        {
            //delegate allocations below - but that's fine as it's not really on the "critical path" (polled relatively infrequently) - and it's much more readable this way
            scheduler.Schedule(repeatAction => _httpListener.BeginGetContext(ar =>
            {
                try
                {
                    var httpListenerContext = _httpListener.EndGetContext(ar);
                    ProcessScrapeRequest(httpListenerContext);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(string.Format("Error in MetricsServer: {0}", e));
                }
                repeatAction.Invoke();
            }, null));
        }

        public void Stop()
        {
            _httpListener.Stop();
            _httpListener.Close();
        }
    }
}