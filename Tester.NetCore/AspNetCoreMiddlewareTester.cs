using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Prometheus;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace tester
{
    /// <summary>
    /// This targets ASP.NET Core 3.
    /// </summary>
    internal class AspNetCoreMiddlewareTester : Tester
    {
        // Sinaled when it is time for the web server to stop.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _webserverTask;

        private IHttpClientFactory _httpClientFactory;

        public override void OnStart()
        {
            _webserverTask =
                WebHost.CreateDefaultBuilder()
                .UseUrls($"http://localhost:{TesterConstants.TesterPort}")
                .ConfigureServices(services =>
                {
                    services.AddHttpClient(Options.DefaultName).UseHttpClientMetrics();
                    services.AddHttpClient("client2").UseHttpClientMetrics();
                    services.AddHttpClient("client3").UseHttpClientMetrics();
                })
                .Configure(app =>
                {
                    _httpClientFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();

                    app.UseMetricServer();

                    app.UseRouting();

                    // We capture metrics URL just for test data.
                    app.UseHttpMetrics(options =>
                    {
                        options.CaptureMetricsUrl = true;

                        options.AddCustomLabel("host", context => context.Request.Host.Host);
                    });
                })
                .ConfigureLogging(logging => logging.ClearProviders())
                .Build()
                .RunAsync(_cts.Token);
        }

        public override void OnTimeToObserveMetrics()
        {
            // Every time we observe metrics, we also asynchronously perform a dummy request for test data.
            StartDummyRequest();

            var httpRequest = (HttpWebRequest)WebRequest.Create($"http://localhost:{TesterConstants.TesterPort}/metrics");
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
        }

        private void StartDummyRequest()
        {
            Task.Run(async delegate
            {
                using var client = _httpClientFactory.CreateClient();
                await client.GetAsync($"http://localhost:{TesterConstants.TesterPort}/api/Dummy");

                using var client2 = _httpClientFactory.CreateClient("client2");
                await client2.GetAsync($"http://localhost:{TesterConstants.TesterPort}/api/Dummy");

                using var client3 = _httpClientFactory.CreateClient("client3");
                await client3.GetAsync($"http://localhost:{TesterConstants.TesterPort}/api/Dummy");
            });
        }

        public override void OnEnd()
        {
            _cts.Cancel();

            try
            {
                _webserverTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override IMetricServer InitializeMetricServer() => null;
    }
}
