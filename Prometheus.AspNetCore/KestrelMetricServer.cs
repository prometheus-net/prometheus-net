using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Prometheus.Advanced;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// A stand-alone Kestrel based metric server that saves you the effort of setting up the ASP.NET Core pipeline.
    /// For all practical purposes, this is just a regular ASP.NET Core server that only serves Prometheus requests.
    /// </summary>
    /// <remarks>
    /// The overall utility of this class is somewhat suspect, considering HttpListener is available everywhere.
    /// If you are not sure you need this, use MetricServer instead.
    /// </remarks>
    public sealed class KestrelMetricServer : MetricHandler
    {
        public KestrelMetricServer(int port, string url = "/metrics", IEnumerable<IOnDemandCollector> onDemandCollectors = null, ICollectorRegistry registry = null, X509Certificate2 certificate = null) : this("+", port, url, onDemandCollectors, registry, certificate)
        {
        }

        public KestrelMetricServer(string hostname, int port, string url = "/metrics", IEnumerable<IOnDemandCollector> onDemandCollectors = null, ICollectorRegistry registry = null, X509Certificate2 certificate = null) : base(onDemandCollectors, registry)
        {
            _hostname = hostname;
            _port = port;
            _url = url;

            _certificate = certificate;
        }

        private readonly string _hostname;
        private readonly int _port;
        private readonly string _url;

        private readonly X509Certificate2 _certificate;

        protected override Task StartServer(CancellationToken cancel)
        {
            var s = _certificate != null ? "s" : "";
            var hostAddress = $"http{s}://{_hostname}:{_port}";

            var builder = WebHost.CreateDefaultBuilder()
                .Configure(app =>
                {
                    // _registry will already be pre-configured by MetricHandler.
                    app.UsePrometheusServer(_url, new IOnDemandCollector[0], _registry);
                });

            if (_certificate != null)
            {
                builder = builder.ConfigureServices(services =>
                    {
                        Action<ListenOptions> configureEndpoint = options =>
                        {
                            options.UseHttps(_certificate);
                        };

                        services.Configure<KestrelServerOptions>(options =>
                        {
                            // In truth, there are more loopback addresses but let's consider this acceptable for now.
                            if (_hostname == "+" || _hostname == "127.0.0.1")
                                options.Listen(IPAddress.Loopback, _port, configureEndpoint);
                            else
                                options.Listen(IPAddress.Any, _port, configureEndpoint);
                        });
                    });
            }
            else
            {
                builder = builder.UseUrls(hostAddress);
            }
                
            return builder.Build().RunAsync(cancel);
        }
    }
}
