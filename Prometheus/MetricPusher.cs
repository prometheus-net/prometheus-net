using System.Diagnostics;
using System.Text;

namespace Prometheus;

/// <summary>
/// A metric server that regularly pushes metrics to a Prometheus PushGateway.
/// </summary>
public class MetricPusher : MetricHandler
{
    private readonly TimeSpan _pushInterval;
    private readonly HttpMethod _method;
    private readonly Uri _targetUrl;
    private readonly Func<HttpClient> _httpClientProvider;

    public MetricPusher(string endpoint, string job, string? instance = null, long intervalMilliseconds = 1000, IEnumerable<Tuple<string, string>>? additionalLabels = null, CollectorRegistry? registry = null, bool pushReplace = false) : this(new MetricPusherOptions
    {
        Endpoint = endpoint,
        Job = job,
        Instance = instance,
        IntervalMilliseconds = intervalMilliseconds,
        AdditionalLabels = additionalLabels,
        Registry = registry,
        ReplaceOnPush = pushReplace,
    })
    {
    }

    public MetricPusher(MetricPusherOptions options)
    {
        if (string.IsNullOrEmpty(options.Endpoint))
            throw new ArgumentNullException(nameof(options.Endpoint));

        if (string.IsNullOrEmpty(options.Job))
            throw new ArgumentNullException(nameof(options.Job));

        if (options.IntervalMilliseconds <= 0)
            throw new ArgumentException("Interval must be greater than zero", nameof(options.IntervalMilliseconds));

        _registry = options.Registry ?? Metrics.DefaultRegistry;

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

        if (!Uri.TryCreate(sb.ToString(), UriKind.Absolute, out var targetUrl) || targetUrl == null)
        {
            throw new ArgumentException("Endpoint must be a valid url", nameof(options.Endpoint));
        }

        _targetUrl = targetUrl;

        _pushInterval = TimeSpan.FromMilliseconds(options.IntervalMilliseconds);
        _onError = options.OnError;

        _method = options.ReplaceOnPush ? HttpMethod.Put : HttpMethod.Post;
    }

    private static readonly HttpClient _singletonHttpClient = new();

    private readonly CollectorRegistry _registry;
    private readonly Action<Exception>? _onError;

    protected override Task StartServer(CancellationToken cancel)
    {
        // Start the server processing loop asynchronously in the background.
        return Task.Run(async delegate
        {
            // We do 1 final push after we get cancelled, to ensure that we publish the final state.
            var pushingFinalState = false;

            while (true)
            {
                // We schedule approximately at the configured interval. There may be some small accumulation for the
                // part of the loop we do not measure but it is close enough to be acceptable for all practical scenarios.
                var duration = ValueStopwatch.StartNew();

                try
                {
                    var httpClient = _httpClientProvider();

                    var request = new HttpRequestMessage
                    {
                        Method = _method,
                        RequestUri = _targetUrl,
                        // We use a copy-pasted implementation of PushStreamContent here to avoid taking a dependency on the old ASP.NET Web API where it lives.
                        Content = new PushStreamContentInternal(async (stream, content, context) =>
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
                        }, PrometheusConstants.ExporterContentTypeValue),
                    };

                    var response = await httpClient.SendAsync(request);

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

                if (cancel.IsCancellationRequested)
                {
                    if (!pushingFinalState)
                    {
                        // Continue for one more loop to push the final state.
                        // We do this because it might be that we were stopped while in the middle of a push.
                        pushingFinalState = true;
                        continue;
                    }
                    else
                    {
                        // Final push completed, time to pack up our things and go home.
                        break;
                    }
                }

                var sleepTime = _pushInterval - duration.GetElapsedTime();

                // Sleep until the interval elapses or the pusher is asked to shut down.
                if (sleepTime > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(sleepTime, cancel);
                    }
                    catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                    {
                        // The task was cancelled.
                        // We continue the loop here to ensure final state gets pushed.
                        pushingFinalState = true;
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
