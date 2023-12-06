using System.Diagnostics;
using System.Net;

namespace Prometheus;

/// <summary>
/// Implementation of a Prometheus exporter that serves metrics using HttpListener.
/// This is a stand-alone exporter for apps that do not already have an HTTP server included.
/// </summary>
public class MetricServer : MetricHandler
{
    private readonly HttpListener _httpListener = new();

    /// <summary>
    /// Only requests that match this predicate will be served by the metric server. This allows you to add authorization checks.
    /// By default (if null), all requests are served.
    /// </summary>
    public Func<HttpListenerRequest, bool>? RequestPredicate { get; set; }

    public MetricServer(int port, string url = "metrics/", CollectorRegistry? registry = null, bool useHttps = false) : this("+", port, url, registry, useHttps)
    {
    }

    public MetricServer(string hostname, int port, string url = "metrics/", CollectorRegistry? registry = null, bool useHttps = false)
    {
        var s = useHttps ? "s" : "";
        _httpListener.Prefixes.Add($"http{s}://{hostname}:{port}/{url}");

        _registry = registry ?? Metrics.DefaultRegistry;
    }

    private readonly CollectorRegistry _registry;

    protected override Task StartServer(CancellationToken cancel)
    {
        // This will ensure that any failures to start are nicely thrown from StartServerAsync.
        _httpListener.Start();

        // Kick off the actual processing to a new thread and return a Task for the processing thread.
        return Task.Factory.StartNew(delegate
        {
            try
            {
                Thread.CurrentThread.Name = "Metric Server";     //Max length 16 chars (Linux limitation)

                while (!cancel.IsCancellationRequested)
                {
                    // There is no way to give a CancellationToken to GCA() so, we need to hack around it a bit.
                    var getContext = _httpListener.GetContextAsync();
                    getContext.Wait(cancel);
                    var context = getContext.Result;

                    // Asynchronously process the request.
                    _ = Task.Factory.StartNew(async delegate
                    {
                        var request = context.Request;
                        var response = context.Response;

                        try
                        {
                            var predicate = RequestPredicate;

                            if (predicate != null && !predicate(request))
                            {
                                // Request rejected by predicate.
                                response.StatusCode = (int)HttpStatusCode.Forbidden;
                                return;
                            }

                            try
                            {
                                // We first touch the response.OutputStream only in the callback because touching
                                // it means we can no longer send headers (the status code).
                                var serializer = new TextSerializer(delegate
                                {
                                    response.ContentType = PrometheusConstants.TextContentTypeWithVersionAndEncoding;
                                    response.StatusCode = 200;
                                    return response.OutputStream;
                                });

                                await _registry.CollectAndSerializeAsync(serializer, cancel);
                                response.OutputStream.Dispose();
                            }
                            catch (ScrapeFailedException ex)
                            {
                                // This can only happen before anything is written to the stream, so it
                                // should still be safe to update the status code and report an error.
                                response.StatusCode = 503;

                                if (!string.IsNullOrWhiteSpace(ex.Message))
                                {
                                    using (var writer = new StreamWriter(response.OutputStream))
                                        writer.Write(ex.Message);
                                }
                            }
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            if (!_httpListener.IsListening)
                                return; // We were shut down.

                            Trace.WriteLine(string.Format("Error in {0}: {1}", nameof(MetricServer), ex));

                            try
                            {
                                response.StatusCode = 500;
                            }
                            catch
                            {
                                // Might be too late in request processing to set response code, so just ignore.
                            }
                        }
                        finally
                        {
                            response.Close();
                        }
                    });
                }
            }
            finally
            {
                _httpListener.Stop();
                // This should prevent any currently processed requests from finishing.
                _httpListener.Close();
            }
        }, TaskCreationOptions.LongRunning);
    }
}
