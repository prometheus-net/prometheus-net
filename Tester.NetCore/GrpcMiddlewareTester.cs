using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Tester;

namespace tester
{
    internal class GrpcMiddlewareTester : Tester
    {
        // Sinaled when it is time for the web server to stop.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _webserverTask;

        public class GreeterService : Greeter.GreeterBase
        {
            public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
            {
                return Task.FromResult(new HelloReply
                {
                    Message = "Hello, Tester!"
                });
            }
        }

        public override void OnStart()
        {
            _webserverTask =
                WebHost.CreateDefaultBuilder()
                .UseUrls($"https://localhost:{TesterConstants.TesterPort}")
                .ConfigureServices(services =>
                {
                    services.AddGrpc();
                })
                .Configure(app =>
                {
                    app.UseMetricServer();

                    app.UseRouting();
                    app.UseGrpcMetrics();

                    app.UseEndpoints(ep =>
                    {
                        ep.MapGrpcService<GreeterService>();
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

            var httpRequest = (HttpWebRequest)WebRequest.Create($"https://localhost:{TesterConstants.TesterPort}/metrics");
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
                try
                {
                    using var channel = GrpcChannel.ForAddress($"https://localhost:{TesterConstants.TesterPort}");
                    var client = new Greeter.GreeterClient(channel);

                    await client.SayHelloAsync(new HelloRequest { Name = "Anonymous" });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
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
