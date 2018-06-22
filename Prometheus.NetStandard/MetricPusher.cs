using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// A metric server that regularly pushes metrics to a Prometheus PushGateway.
    /// </summary>
    public class MetricPusher : MetricHandler
    {
        private readonly string _endpoint;
        private readonly string _job;
        private readonly string _instance;
        private readonly IEnumerable<Tuple<string, string>> _additionalLabels;

        /// <summary>
        /// Used as input for the srape handler, so it generates the output in the expected format.
        /// Not used in PushGateway communications.
        /// </summary>
        private const string ContentType = "text/plain; version=0.0.4";

        private readonly TimeSpan _pushInterval;
        private readonly MetricPushService _pushService;

        public MetricPusher(string endpoint, string job, string instance = null, long intervalMilliseconds = 1000, IEnumerable<Tuple<string, string>> additionalLabels = null, ICollectorRegistry registry = null) : base(registry)
        {
            _endpoint = endpoint;
            _job = job;
            _instance = instance;
            _additionalLabels = additionalLabels;
            _pushService = new MetricPushService();
            if (intervalMilliseconds <= 0)
            {
                throw new ArgumentException("Interval must be greater than zero", "intervalMilliseconds");
            }

            _pushInterval = TimeSpan.FromMilliseconds(intervalMilliseconds);
        }

        protected override Task StartServer(CancellationToken cancel)
        {
            // Kick off the actual processing to a new thread and return a Task for the processing thread.
            return Task.Run(async delegate
            {
                while (true)
                {
                    // We schedule approximately at the configured interval. There may be some small accumulation for the
                    // part of the loop we do not measure but it is close enough to be acceptable for all practical scenarios.
                    var duration = Stopwatch.StartNew();

                    try
                    {
                        var metrics = _registry.CollectAll();
                        await _pushService
                            .PushAsync(metrics, _endpoint, _job, _instance, additionalLabels: _additionalLabels)
                            .ConfigureAwait(false);
                    }
                    catch (ScrapeFailedException ex)
                    {
                        Trace.WriteLine($"Skipping metrics push due to failed scrape: {ex.Message}");
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Trace.WriteLine(string.Format("Error in MetricPusher: {0}", ex));
                    }

                    // We always stop after pushing metrics, to ensure that the latest state is flushed when told to stop.
                    if (cancel.IsCancellationRequested)
                        break;

                    var sleepTime = _pushInterval - duration.Elapsed;

                    // Sleep until the interval elapses or the pusher is asked to shut down.
                    if (sleepTime > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(sleepTime, cancel);
                        }
                        catch (OperationCanceledException)
                        {
                            // The task was cancelled.
                            // We continue the loop here to ensure final state gets pushed.
                            continue;
                        }
                    }
                }
            });
        }
    }
}
