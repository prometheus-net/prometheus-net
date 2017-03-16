#if NETSTANDARD1_3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reactive.Concurrency;
using Prometheus.Advanced;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace Prometheus
{
    public class MetricServer : MetricHandler
    {
        private IDisposable _schedulerDelegate;
        private readonly string hostAddress;
        private IWebHost host;
        public bool IsRunning => host != null;
        private X509Certificate2 certificate;

        public MetricServer(int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null,
            bool useHttps = false, X509Certificate2 certificate = null) :
            this("+", port, standardCollectors, url, registry, useHttps)
        {
        }

        public MetricServer(string hostname, int port, IEnumerable<IOnDemandCollector> standardCollectors = null, string url = "metrics/", ICollectorRegistry registry = null,
            bool useHttps = false, X509Certificate2 certificate = null) : base(standardCollectors, registry)
        {
            if (useHttps && certificate == null) {
                throw new ArgumentNullException(nameof(certificate), $"{nameof(certificate)} is required when using https");
            }
            this.certificate = certificate;
            var s = useHttps ? "s" : "";
            hostAddress = $"http{s}://{hostname}:{port}/{url}";
            if (_registry == DefaultCollectorRegistry.Instance) {
                // Default to DotNetStatsCollector if none speified
                // For no collectors, pass an empty collection
                if (standardCollectors == null)
                    standardCollectors = new[] { new DotNetStatsCollector() };

                DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(standardCollectors);
            }
        }

        protected override IDisposable StartLoop(IScheduler scheduler)
        {
            if (host != null) {
                throw new Exception("Server is already running.");
            }
            var configBuilder = new ConfigurationBuilder();
            configBuilder.Properties["parent"] = this;
            var config = configBuilder.Build();

            host = new WebHostBuilder()
                .UseConfiguration(config)
                 .UseKestrel(options =>
                 {
                     if (certificate != null) {
                         options.UseHttps(certificate);
                     }
                 })
                 .UseUrls(hostAddress)
                 .ConfigureServices(services =>
                 {
                     services.AddSingleton<IStartup>(new Startup(_registry));
                 })
                 .Build();

            host.Start();

            return host;
        }

        protected override void StopInner()
        {
            if (host != null) {
                host.Dispose();
                host = null;
            }
        }

        public class Startup : IStartup
        {
            private readonly ICollectorRegistry _registry;
            public IConfigurationRoot Configuration { get; }
            public Startup(ICollectorRegistry _registry)
            {
                this._registry = _registry;
                var builder = new ConfigurationBuilder();
                Configuration = builder.Build();
            }
            public IServiceProvider ConfigureServices(IServiceCollection services)
            {
                return services.BuildServiceProvider();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.Run(context => {
                    var response = context.Response;
                    var request = context.Request;
                    response.StatusCode = 200;

                    var acceptHeader = request.Headers["Accept"];
                    var contentType = ScrapeHandler.GetContentType(acceptHeader);
                    response.ContentType = contentType;

                    using (var outputStream = response.Body) {
                        var collected = _registry.CollectAll();
                        ScrapeHandler.ProcessScrapeRequest(collected, contentType, outputStream);
                    };
                    return Task.FromResult(true);
                });
            }
        }
    }
}

#endif