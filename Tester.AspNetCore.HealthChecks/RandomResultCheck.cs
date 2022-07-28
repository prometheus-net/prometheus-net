using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tester.AspNetCore.HealthChecks
{
    public sealed class RandomResultCheck : IHealthCheck
    {
        private static readonly Random _random = new Random();

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            switch (_random.Next(3))
            {
                case 0:
                    return Task.FromResult(HealthCheckResult.Unhealthy());
                case 1:
                    return Task.FromResult(HealthCheckResult.Degraded());
                default:
                    return Task.FromResult(HealthCheckResult.Healthy());
            }
        }
    }
}
