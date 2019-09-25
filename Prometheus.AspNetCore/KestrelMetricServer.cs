using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
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
        public KestrelMetricServer(int port, string url = "/metrics", CollectorRegistry? registry = null, X509Certificate2? certificate = null) : this("+", port, url, registry, certificate)
        {
        }

        public KestrelMetricServer(string hostname, int port, string url = "/metrics", CollectorRegistry? registry = null, X509Certificate2? certificate = null) : base(registry)
        {
            _hostname = hostname;
            _port = port;
            _url = url;

            _certificate = certificate;
        }

        private readonly string _hostname;
        private readonly int _port;
        private readonly string _url;

        private readonly X509Certificate2? _certificate;

        protected override Task StartServer(CancellationToken cancel)
        {
            var s = _certificate != null ? "s" : "";
            var hostAddress = $"http{s}://{_hostname}:{_port}";

            // If the caller needs to customize any of this, they can just set up their own web host and inject the middleware.
            var builder = new WebHostBuilder()
                .UseKestrel()
                .UseIISIntegration()
                .Configure(app =>
                {
                    // _registry will already be pre-configured by MetricHandler.
                    app.UseMetricServer(_url, _registry);

                    // If there is any URL prefix, we just redirect people going to root URL to our prefix.
                    if (!string.IsNullOrWhiteSpace(_url.Trim('/')))
                    {
                        app.MapWhen(context => context.Request.Path.Value.Trim('/') == "",
                            configuration =>
                            {
                                configuration.Use((context, next) =>
                                {
                                    context.Response.Redirect(_url);
                                    return Task.CompletedTask;
                                });
                            });
                    }
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
                            options.Listen(IPAddress.Any, _port, configureEndpoint);
                        });
                    });
            }
            else
            {
                builder = builder.UseUrls(hostAddress);
            }

            var webHost = builder.Build();
            webHost.Start();

            return webHost.WaitForShutdownAsync(cancel);
        }
    }
}
