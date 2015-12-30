using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reactive.Concurrency;
using Prometheus.Advanced;

namespace Prometheus
{
    public interface IMetricServer
    {
        void Start(IScheduler scheduler = null);
        void Stop();
    }

    public class MetricServer : IMetricServer
    {
        readonly HttpListener _httpListener = new HttpListener();
        readonly ICollectorRegistry _registry;
        
        public MetricServer(int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null) : this("+", port, standardCollectors, url, registry)
        {
        }

        public MetricServer(string hostname, int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null)
        {
            _registry = registry ?? DefaultCollectorRegistry.Instance;
            _httpListener.Prefixes.Add(string.Format("http://{0}:{1}/{2}", hostname, port, url));
            if (_registry == DefaultCollectorRegistry.Instance)
            {
                // Default to perf counter collectors if none speified
                // For no collectors, pass an empty collection
                if (standardCollectors == null)
                    standardCollectors = new[] {new PerfCounterCollector()};

                DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(standardCollectors);
            }
        }

        public void Start(IScheduler scheduler = null)
        {
            _httpListener.Start();

            StartLoop(scheduler ?? Scheduler.Default);
        }

        private void StartLoop(IScheduler scheduler)
        {
            //delegate allocations below - but that's fine as it's not really on the "critical path" (polled relatively infrequently) - and it's much more readable this way
            scheduler.Schedule(repeatAction => _httpListener.BeginGetContext(ar =>
            {
                try
                {
                    var httpListenerContext = _httpListener.EndGetContext(ar);
                    var request = httpListenerContext.Request;
                    var response = httpListenerContext.Response;

                    response.StatusCode = 200;
                    
                    var acceptHeader = request.Headers.Get("Accept");
                    var acceptHeaders = acceptHeader == null ? null : acceptHeader.Split(',');
                    var contentType = ScrapeHandler.GetContentType(acceptHeaders);
                    response.ContentType = contentType;

                    using (var outputStream = response.OutputStream)
                    {
                        var collected = _registry.CollectAll();
                        ScrapeHandler.ProcessScrapeRequest(collected, contentType, outputStream);
                    }

                    response.Close();
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