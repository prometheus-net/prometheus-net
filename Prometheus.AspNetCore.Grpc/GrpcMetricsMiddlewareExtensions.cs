using Microsoft.AspNetCore.Builder;

namespace Prometheus;

public static class GrpcMetricsMiddlewareExtensions
{
    /// <summary>
    /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed gRPC requests.
    /// </summary>
    public static IApplicationBuilder UseGrpcMetrics(this IApplicationBuilder app,
        Action<GrpcMiddlewareExporterOptions> configure)
    {
        var options = new GrpcMiddlewareExporterOptions();
        configure?.Invoke(options);
        app.UseGrpcMetrics(options);
        return app;
    }

    /// <summary>
    /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed gRPC requests.
    /// </summary>
    public static IApplicationBuilder UseGrpcMetrics(this IApplicationBuilder app,
        GrpcMiddlewareExporterOptions? options = null)
    {
        options ??= new GrpcMiddlewareExporterOptions();

        if (options.RequestCount.Enabled)
        {
            app.UseMiddleware<GrpcRequestCountMiddleware>(options.RequestCount);
        }

        if (options.RequestDuration.Enabled)
        {
            app.UseMiddleware<GrpcRequestDurationMiddleware>(options.RequestDuration);
        }

        return app;
    }
}
