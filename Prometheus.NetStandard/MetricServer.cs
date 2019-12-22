using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Implementation of a Prometheus exporter that serves metrics using HttpListener.
    /// This is a stand-alone exporter for apps that do not already have an HTTP server included.
    /// </summary>
    public class MetricServer : MetricHandler
    {
        private readonly HttpListener _httpListener = new HttpListener();

        public MetricServer(int port, string url = "metrics/", CollectorRegistry? registry = null, bool useHttps = false) : this("+", port, url, registry, useHttps)
        {
        }

        public MetricServer(string hostname, int port, string url = "metrics/", CollectorRegistry? registry = null, bool useHttps = false) : base(registry)
        {
            var s = useHttps ? "s" : "";
            _httpListener.Prefixes.Add($"http{s}://{hostname}:{port}/{url}");
        }

        protected override Task StartServer(CancellationToken cancel)
        {
            // This will ensure that any failures to start are nicely thrown from StartServerAsync.
            _httpListener.Start();

            // Kick off the actual processing to a new thread and return a Task for the processing thread.
            return Task.Factory.StartNew(delegate
            {
                try
                {
                    while (!cancel.IsCancellationRequested)
                    {
                        // There is no way to give a CancellationToken to GCA() so, we need to hack around it a bit.
                        var getContext = _httpListener.GetContextAsync();
                        getContext.Wait(cancel);
                        var context = getContext.Result;

                        // Kick the request off to a background thread for processing.
                        _ = Task.Factory.StartNew(async delegate
                          {
                              var request = context.Request;
                              var response = context.Response;

                              try
                              {
                                  try
                                  {
                                      // We first touch the response.OutputStream only in the callback because touching
                                      // it means we can no longer send headers (the status code).
                                      var serializer = new TextSerializer(delegate
                                        {
                                            response.ContentType = PrometheusConstants.ExporterContentType;
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

                                  Trace.WriteLine(string.Format("Error in MetricsServer: {0}", ex));

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
                          }, TaskCreationOptions.LongRunning);

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
}
