using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Prometheus;

public static class HealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder ForwardToPrometheus(this IHealthChecksBuilder builder, PrometheusHealthCheckPublisherOptions? options = null)
    {
        builder.Services.AddSingleton<IHealthCheckPublisher, PrometheusHealthCheckPublisher>(provider => new PrometheusHealthCheckPublisher(options));

        return builder;
    }
}
