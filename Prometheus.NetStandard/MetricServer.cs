using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
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
        private class RawMetric
        {
            private string _name = "";
            private string _help = "";
            private string _type = "";
            private Dictionary<string, double> _metrics = new Dictionary<string, double>();

            public string Name()
            {
                return _name;
            }

            public void SetName(string name)
            {
                _name = name;
            }

            public void SetHelp(string help)
            {
                _help = help;
            }

            public void SetType(string type)
            {
                _type = type;
            }

            public void SetMetric(string key, double value)
            {
                if (_metrics.ContainsKey(key))
                {
                    if (_type != "gauge")
                    {
                        _metrics[key] = _metrics[key] + value;
                    }
                    else
                    {
                        _metrics[key] = value;
                    }
                }
                else
                {
                    _metrics.Add(key, value);
                }
            }

            override public string ToString()
            {
                string str = "";
                str += "# HELP " + _name + " " + _help + "\n";
                str += "# TYPE " + _name + " " + _type + "\n";
                foreach (var metric in _metrics)
                {
                    str += metric.Key + " " + metric.Value.ToString() + "\n";
                }
                return str;
            }
        }

        private List<RawMetric> _rawMetrics = new List<RawMetric>();
        private Regex _regexHelp = new Regex(@"^# HELP (?<name>\w+) (?<help>.+)$");
        private Regex _regexType = new Regex(@"^# TYPE (?<name>\w+) (?<type>.+)$");
        private Regex _regexMetric = new Regex(@"^(?<key>.+) (?<value>.+)$");
        private string _job = "";

        private readonly HttpListener _httpListener = new HttpListener();

        /// <summary>
        /// Only requests that match this predicate will be served by the metric server. This allows you to add authorization checks.
        /// By default (if null), all requests are served.
        /// </summary>
        public Func<HttpListenerRequest, bool>? RequestPredicate { get; set; }

        public MetricServer(int port, string url = "metrics/", string job = "", CollectorRegistry? registry = null, bool useHttps = false) : this("+", port, url, job, registry, useHttps)
        {
        }

        public MetricServer(string hostname, int port, string url = "metrics/", string job = "", CollectorRegistry? registry = null, bool useHttps = false) : base(registry)
        {
            _job = job;
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

                            if ((request.HttpMethod == "POST") && (request.Url.AbsolutePath.EndsWith("/metrics/job/" + _job)))
                            {
                                try
                                {
                                    response.StatusCode = 200;
                                    if (!request.HasEntityBody)
                                    {
                                        Trace.WriteLine("No client data was sent with the request.");
                                        return;
                                    }
                                    Stream body = request.InputStream;
                                    System.Text.Encoding encoding = request.ContentEncoding;
                                    StreamReader reader = new StreamReader(body, encoding);
                                    if (request.ContentType != null)
                                    {
                                        Trace.WriteLine("Client data content type {0}", request.ContentType);
                                    }
                                    HandleRawMetrics(reader);
                                    body.Close();
                                    reader.Close();
                                }
                                catch (Exception ex)
                                {
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
                            }
                            else
                            {
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
                                            response.ContentType = PrometheusConstants.ExporterContentType;
                                            response.StatusCode = 200;
                                            return response.OutputStream;
                                        });

                                        var buffer = GetRawMetricsBytes();
                                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancel);

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

        private RawMetric GetRawMetric(string name)
        {
            foreach (RawMetric _rawMetric in _rawMetrics)
            {
                if (_rawMetric.Name() == name)
                {
                    return _rawMetric;
                }
            }
            RawMetric rawMetric = new RawMetric();
            _rawMetrics.Add(rawMetric);
            return rawMetric;
        }

        private void HandleRawMetrics(StreamReader reader)
        {
            while (reader.Peek() >= 0)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                // Start of a metric block.
                if (line.StartsWith("# HELP"))
                {
                    // Parse help.
                    Match m = _regexHelp.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    string name = m.Result("${name}");
                    string help = m.Result("${help}");

                    // Get existing metric or new.
                    RawMetric rawMetric = GetRawMetric(name);
                    rawMetric.SetName(name);
                    rawMetric.SetHelp(help);

                    // Parse type.
                    if (reader.Peek() != 35) // '#'
                    {
                        continue;
                    }
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    m = _regexType.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    string type = m.Result("${type}");
                    rawMetric.SetType(type);

                    // Parse metrics.
                    while (reader.Peek() != 35) // '#'
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }
                        m = _regexMetric.Match(line);
                        if (!m.Success)
                        {
                            continue;
                        }
                        string key = m.Result("${key}");
                        double value = double.Parse(m.Result("${value}"));
                        rawMetric.SetMetric(key, value);
                    }
                }
            }
        }

        private byte[] GetRawMetricsBytes()
        {
            string str = "";
            foreach (RawMetric rawMetric in _rawMetrics)
            {
                str += rawMetric.ToString();
            }
            return PrometheusConstants.ExportEncoding.GetBytes(str);
        }
    }
}
