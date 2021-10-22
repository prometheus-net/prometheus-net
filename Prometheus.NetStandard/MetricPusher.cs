using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private readonly TimeSpan _pushInterval;
        private readonly Uri _targetUrl;
        private readonly Func<HttpClient> _httpClientProvider;

        public MetricPusher(string endpoint, string job, string? instance = null, long intervalMilliseconds = 1000, IEnumerable<Tuple<string, string>>? additionalLabels = null, CollectorRegistry? registry = null) : this(new MetricPusherOptions
        {
            Endpoint = endpoint,
            Job = job,
            Instance = instance,
            IntervalMilliseconds = intervalMilliseconds,
            AdditionalLabels = additionalLabels,
            Registry = registry
        })
        {
        }

        public MetricPusher(MetricPusherOptions options) : base(options.Registry)
        {
            if (string.IsNullOrEmpty(options.Endpoint))
                throw new ArgumentNullException(nameof(options.Endpoint));

            if (string.IsNullOrEmpty(options.Job))
                throw new ArgumentNullException(nameof(options.Job));

            if (options.IntervalMilliseconds <= 0)
                throw new ArgumentException("Interval must be greater than zero", nameof(options.IntervalMilliseconds));

            _httpClientProvider = options.HttpClientProvider ?? (() => _singletonHttpClient);

            StringBuilder sb = new StringBuilder(string.Format("{0}/job/{1}", options.Endpoint!.TrimEnd('/'), options.Job));
            if (!string.IsNullOrEmpty(options.Instance))
                sb.AppendFormat("/instance/{0}", options.Instance);

            if (options.AdditionalLabels != null)
            {
                foreach (var pair in options.AdditionalLabels)
                {
                    if (pair == null || string.IsNullOrEmpty(pair.Item1) || string.IsNullOrEmpty(pair.Item2))
                        throw new NotSupportedException($"Invalid {nameof(MetricPusher)} additional label: ({pair?.Item1}):({pair?.Item2})");

                    sb.AppendFormat("/{0}/{1}", pair.Item1, pair.Item2);
                }
            }

            if (!Uri.TryCreate(sb.ToString(), UriKind.Absolute, out _targetUrl))
            {
                throw new ArgumentException("Endpoint must be a valid url", "endpoint");
            }

            _pushInterval = TimeSpan.FromMilliseconds(options.IntervalMilliseconds);
            _onError = options.OnError;
        }

        private static readonly HttpClient _singletonHttpClient = new HttpClient();

        private readonly Action<Exception>? _onError;

        protected override Task StartServer(CancellationToken cancel)
        {
            // Start the server processing loop asynchronously in the background.
            return Task.Run(async delegate
            {
                while (true)
                {
                    // We schedule approximately at the configured interval. There may be some small accumulation for the
                    // part of the loop we do not measure but it is close enough to be acceptable for all practical scenarios.
                    var duration = ValueStopwatch.StartNew();

                    try
                    {
                        var httpClient = _httpClientProvider();

                        // We use a copy-pasted implementation of PushStreamContent here to avoid taking a dependency on the old ASP.NET Web API where it lives.
                        var response = await httpClient.PostAsync(_targetUrl, new PushStreamContentInternal(async (stream, content, context) =>
                        {
                            try
                            {
                                // Do not pass CT because we only want to cancel after pushing, so a flush is always performed.
                                await _registry.CollectAndExportAsTextAsync(stream, default);
                            }
                            finally
                            {
                                stream.Close();
                            }
                        }, PrometheusConstants.ExporterContentTypeValue));

                        // If anything goes wrong, we want to get at least an entry in the trace log.
                        response.EnsureSuccessStatusCode();
                    }
                    catch (ScrapeFailedException ex)
                    {
                        // We do not consider failed scrapes a reportable error since the user code that raises the failure should be the one logging it.
                        Trace.WriteLine($"Skipping metrics push due to failed scrape: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        HandleFailedPush(ex);
                    }

                    // We stop only after pushing metrics, to ensure that the latest state is flushed when told to stop.
                    if (cancel.IsCancellationRequested)
                        break;

                    var sleepTime = _pushInterval - duration.GetElapsedTime();

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

        private void HandleFailedPush(Exception ex)
        {
            if (_onError != null)
            {
                // Asynchronous because we don't trust the callee to be fast.
                Task.Run(() => _onError(ex));
            }
            else
            {
                // If there is no error handler registered, we write to trace to at least hopefully get some attention to the problem.
                Trace.WriteLine(string.Format("Error in MetricPusher: {0}", ex));
            }
        }
    }
}
