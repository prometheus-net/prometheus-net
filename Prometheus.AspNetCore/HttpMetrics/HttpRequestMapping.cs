using System;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestMapping
    {
        public string LabelName { get; }
        public Func<HttpContext, string> GetValue { get; }

        public HttpRequestMapping(string labelName, Func<HttpContext, string> getValue)
        {
            Collector.ValidateLabelName(labelName);
            LabelName = labelName;
            GetValue = getValue;
        }
    }
}