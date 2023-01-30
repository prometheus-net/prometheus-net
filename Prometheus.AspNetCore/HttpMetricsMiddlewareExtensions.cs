using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Prometheus.HttpMetrics;

namespace Prometheus;

public static class HttpMetricsMiddlewareExtensions
{
    /// <summary>
    /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed HTTP requests.
    /// 
    /// Call this after .UseRouting().
    /// </summary>
    public static IApplicationBuilder UseHttpMetrics(this IApplicationBuilder app,
        Action<HttpMiddlewareExporterOptions> configure)
    {
        var options = new HttpMiddlewareExporterOptions();

        configure?.Invoke(options);

        app.UseHttpMetrics(options);

        return app;
    }

    /// <summary>
    /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed HTTP requests.
    /// 
    /// Call this after .UseRouting().
    /// </summary>
    public static IApplicationBuilder UseHttpMetrics(this IApplicationBuilder app,
        HttpMiddlewareExporterOptions? options = null)
    {
        options = options ?? new HttpMiddlewareExporterOptions();

        if (app.ApplicationServices.GetService<PageLoader>() != null)
        {
            // If Razor Pages is enabled, we will automatically add a "page" route parameter to represent it. We do this only if no custom metric is used.
            // If a custom metric is used, we still allow "page" label to be present and automatically fill it with the Razor Pages route parameter
            // unless there is a custom label with this name added, in which case the custom label takes priority.

            options.InProgress.IncludePageLabelInDefaultsInternal = true;
            options.RequestCount.IncludePageLabelInDefaultsInternal = true;
            options.RequestDuration.IncludePageLabelInDefaultsInternal = true;
        }

        void ApplyConfiguration(IApplicationBuilder builder)
        {
            builder.UseMiddleware<CaptureRouteDataMiddleware>();

            if (options.InProgress.Enabled)
                builder.UseMiddleware<HttpInProgressMiddleware>(options.InProgress);
            if (options.RequestCount.Enabled)
                builder.UseMiddleware<HttpRequestCountMiddleware>(options.RequestCount);
            if (options.RequestDuration.Enabled)
                builder.UseMiddleware<HttpRequestDurationMiddleware>(options.RequestDuration);
        }

        if (options.CaptureMetricsUrl)
            ApplyConfiguration(app);
        else
            app.UseWhen(context => context.Request.Path != "/metrics", ApplyConfiguration);

        return app;
    }
}