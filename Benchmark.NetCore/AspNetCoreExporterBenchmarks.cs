using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// We start up a real(ish) ASP.NET Core web server stack to measure exporter performance end to end.
/// </summary>
[MemoryDiagnoser]
public class AspNetCoreExporterBenchmarks
{
    static AspNetCoreExporterBenchmarks()
    {
        // We use the global singleton metrics registry here, so just populate it with some data.
        // Not too much data - we care more about the overhead and do not want to just inflate the numbers.
        for (var i = 0; i < 5; i++)
            Metrics.CreateGauge("dummy_metric_" + i, "For benchmark purposes", "label1", "label2", "label3").WithLabels("1", "2", "3").Inc();
    }

    [GlobalSetup]
    public void Setup()
    {
        var builder = new WebHostBuilder().UseStartup<EntryPoint>();
        _server = new TestServer(builder);
        _client = _server.CreateClient();

        // Warmup the ASP.NET Core stack to avoid measuring the ASP.NET Core web server itself in benchmarks.
        _client.GetAsync("/metrics").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _server?.Dispose();
    }

    private TestServer _server;
    private HttpClient _client;

    private sealed class EntryPoint
    {
#pragma warning disable CA1822 // Mark members as static
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseHttpMetrics();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();

                endpoints.MapGet("ok", context =>
                {
                    context.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });
        }
#pragma warning restore CA1822 // Mark members as static
    }

    [Benchmark]
    public async Task GetMetrics()
    {
        await _client.GetAsync("/metrics");
    }

    [Benchmark]
    public async Task Get200Ok()
    {
        await _client.GetAsync("/ok");
    }
}
