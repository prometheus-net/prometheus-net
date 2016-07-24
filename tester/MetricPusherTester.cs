using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using Prometheus;

namespace tester
{
    class MetricPusherTester : Tester
    {
        private IDisposable _schedulerDelegate;
        private HttpListener _httpListener;

        public override IMetricServer InitializeMetricHandler()
        {
            return new MetricPusher(endpoint: "http://localhost:9091/metrics", job: "some_job");
        }

        public override void OnStart()
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://localhost:9091/");
            _httpListener.Start();
            _schedulerDelegate = Scheduler.Default.Schedule(
                action =>
                {
                    try
                    {
                        if (!_httpListener.IsListening)
                        {
                            return;
                        }
                        var httpListenerContext = _httpListener.GetContext();
                        var request = httpListenerContext.Request;
                        var response = httpListenerContext.Response;

                        PrintRequestDetails(request.Url);

                        string body;
                        using (var reader = new StreamReader(request.InputStream))
                        {
                            body = reader.ReadToEnd();
                        }
                        Console.WriteLine(body);
                        response.StatusCode = 204;
                        response.Close();
                        action.Invoke();
                    }
                    catch (HttpListenerException)
                    {
                        // Ignore possible exception at the end of the test
                    }
                });
        }

        public override void OnEnd()
        {
            _httpListener.Stop();
            _httpListener.Close();
            _schedulerDelegate.Dispose();
        }

        private void PrintRequestDetails(Uri requestUrl)
        {
            var segments = requestUrl.Segments;
            int idx;
            if (segments.Length < 2 || (idx = Array.IndexOf(segments, "job/")) < 0)
            {
                Console.WriteLine("# Unexpected label information");
                return;
            }
            StringBuilder sb = new StringBuilder("#");
            for (int i = idx; i < segments.Length; i++)
            {
                if (i == segments.Length - 1)
                {
                    continue;
                }
                sb.AppendFormat(" {0}: {1} |", segments[i].TrimEnd('/'), segments[++i].TrimEnd('/'));
            }
            Console.WriteLine(sb.ToString().TrimEnd('|'));
        }
    }
}
