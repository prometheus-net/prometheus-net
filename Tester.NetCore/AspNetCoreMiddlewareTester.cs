using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

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

                    // Depending on whether this is here or not, the "page" label is added to default HTTP request metrics.
                    services.AddRazorPages();
                })
                .Configure(app =>
                {
                    _httpClientFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();

                    // Legacy approach. Still works but we prefer endpoints mapping.
                    //app.UseMetricServer();

                    app.UseRouting();

                    // We capture metrics URL just for test data.
                    app.UseHttpMetrics(options =>
                    {
                        options.CaptureMetricsUrl = true;

                        options.AddCustomLabel("host", context => context.Request.Host.Host);
                    });

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMetrics();
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

            var text = _httpClientFactory.CreateClient().GetStringAsync($"http://localhost:{TesterConstants.TesterPort}/metrics").Result;
            Console.WriteLine(text);
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
