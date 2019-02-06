using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Prometheus;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace tester
{
    // Works ONLY on Tester.NetCore because Kestrel is a pain to get set up on NetFramework, so let's not bother.
    // You will get some libuv related error if you try to use Tester.NetFramework.
    internal class AspNetCoreMiddlewareTester : Tester
    {
        // Sinaled when it is time for the web server to stop.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _webserverTask;

        public override void OnStart()
        {
            _webserverTask =
                WebHost.CreateDefaultBuilder()
                .UseUrls($"http://localhost:{TesterConstants.TesterPort}")
                .Configure(app => app.UseMetricServer())
                .ConfigureLogging(logging => logging.ClearProviders())
                .Build()
                .RunAsync(_cts.Token);
        }

        public override void OnTimeToObserveMetrics()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create($"http://localhost:{TesterConstants.TesterPort}/metrics");
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
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
