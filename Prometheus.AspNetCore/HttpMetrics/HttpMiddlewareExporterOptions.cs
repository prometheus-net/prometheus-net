using System;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpMiddlewareExporterOptions
    {
        public HttpInProgressOptions InProgress { get; set; } = new HttpInProgressOptions();
        public HttpRequestCountOptions RequestCount { get; set; } = new HttpRequestCountOptions();
        public HttpRequestDurationOptions RequestDuration { get; set; } = new HttpRequestDurationOptions();

        /// <summary>
        /// Adds an additional route parameter to all the HTTP metrics.
        /// 
        /// Helper method to avoid manually adding it to each one.
        /// </summary>
        public void AddRouteParameter(HttpRouteParameterMapping mapping)
        {
            InProgress.AdditionalRouteParameters.Add(mapping);
            RequestCount.AdditionalRouteParameters.Add(mapping);
            RequestDuration.AdditionalRouteParameters.Add(mapping);
        }

        public void SetIgnoreCondition(Func<HttpContext, bool> ignoreCondition)
        {
            InProgress.IgnoreCondition = ignoreCondition;
            RequestCount.IgnoreCondition = ignoreCondition;
            RequestDuration.IgnoreCondition = ignoreCondition;
        }
    }
}