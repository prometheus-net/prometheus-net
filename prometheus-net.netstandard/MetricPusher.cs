using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using Prometheus.Advanced;

namespace Prometheus
{
    public class MetricPusher : MetricHandler
    {
        private const string ContentType = "text/plain; version=0.0.4";
        private readonly TimeSpan _schedulerInterval;
        private readonly Uri _endpoint;
        private readonly object _syncObj = new object();

        public MetricPusher(string endpoint, string job, string instance = null, long intervalMilliseconds = 1000, IEnumerable<Tuple<string, string>> additionalLabels = null, IEnumerable<IOnDemandCollector> standardCollectors = null, ICollectorRegistry registry = null) : base(standardCollectors, registry)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException("endpoint");
            }
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException("job");
            }
            if (intervalMilliseconds <= 0)
            {
                throw new ArgumentException("Interval must be greater than zero", "intervalMilliseconds");
            }
            StringBuilder sb = new StringBuilder(string.Format("{0}/job/{1}", endpoint.TrimEnd('/'), job));
            if (!string.IsNullOrEmpty(instance))
            {
                sb.AppendFormat("/instance/{0}", instance);
            }
            if (additionalLabels != null)
            {
                foreach (var pair in additionalLabels)
                {
                    if (pair == null || string.IsNullOrEmpty(pair.Item1) || string.IsNullOrEmpty(pair.Item2))
                    {
                        Trace.WriteLine("Ignoring invalid label set");
                        continue;
                    }
                    sb.AppendFormat("/{0}/{1}", pair.Item1, pair.Item2);
                }
            }
            if (!Uri.TryCreate(sb.ToString(), UriKind.Absolute, out _endpoint))
            {
                throw new ArgumentException("Endpoint must be a valid url", "endpoint");
            }

            _schedulerInterval = TimeSpan.FromMilliseconds(intervalMilliseconds);
        }

        protected override IDisposable StartLoop(IScheduler scheduler)
        {
            return scheduler.SchedulePeriodic(_schedulerInterval, SendMetrics);
        }

        private void SendMetrics()
        {
            if (Monitor.TryEnter(_syncObj))
            {
                try
                {
                    using (var stream = new MemoryStream())
                    {
                        ScrapeHandler.ProcessScrapeRequest(_registry.CollectAll(), ContentType, stream);
                        using (var client = new NetworkClient())
                        {
                            client.UploadData(_endpoint, stream.ToArray());
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(string.Format("Exception in send metrics: {0}", e));
                }
                finally
                {
                    Monitor.Exit(_syncObj);
                }
            }
        }
        
        protected override void StopInner()
        {
            // Flush unsaved metrics;  especially important for short jobs which don't have time to push anything at all
            SendMetrics();
        }       
    }
}
