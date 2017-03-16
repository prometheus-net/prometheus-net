#if NET40 || NET45

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reactive.Concurrency;
using Prometheus.Advanced;

namespace Prometheus
{
    public class MetricServer : MetricHandler
    {
        readonly HttpListener _httpListener = new HttpListener();
        
        public MetricServer(int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null, bool useHttps = false) : this("+", port, standardCollectors, url, registry, useHttps)
        {
        }

        public MetricServer(string hostname, int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null, bool useHttps = false) : base(standardCollectors, registry)
        {
            var s = useHttps ? "s" : "";
            _httpListener.Prefixes.Add($"http{s}://{hostname}:{port}/{url}");
        }

        protected override IDisposable StartLoop(IScheduler scheduler)
        {
            _httpListener.Start();
            //delegate allocations below - but that's fine as it's not really on the "critical path" (polled relatively infrequently) - and it's much more readable this way
            return scheduler.Schedule(
                repeatAction =>
                {
                    try
                    {
                        _httpListener.BeginGetContext(ar =>
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
                        }, null);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(string.Format("Error in MetricsServer: {0}", e));
                    }
                }
            );
        }

        protected override void StopInner()
        {
            base.StopInner();
            _httpListener.Stop();
            _httpListener.Close();
        }
    }
}

#endif