using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Prometheus.Advanced;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestMiddlewareBase<T, TMetric> where T : Collector<TMetric> where TMetric : Child, new()
    {
        private readonly Dictionary<string, string> _labelData;
        private readonly string[] _labelNames;

        public HttpRequestMiddlewareBase(T collector)
        {
            if (collector == null || !LabelsAreValid(collector)) throw new ArgumentException(nameof(collector)); 
            _labelNames = collector.LabelNames;
            _labelData = _labelNames.ToDictionary(key => key);
        }

        protected string[] GetLabelData(HttpContext context)
        {
            var routeData = context.GetRouteData();

            if (routeData != null)
            {
                UpdateMetricValueIfExists("method", context.Request.Method);
                UpdateMetricValueIfExists("code", context.Response.StatusCode.ToString());
                UpdateMetricValueIfExists("action", routeData.Values["Action"] as string);
                UpdateMetricValueIfExists("controller", routeData.Values["Controller"] as string);

                return _labelNames.Where(_labelData.ContainsKey).Select(x => _labelData[x]).ToArray();
            }

            return null;
        }
        
        private bool LabelsAreValid(T counter)
        {
            if (!_allowedLabelNames.IsSupersetOf(counter.LabelNames)) return false;

            return true;
        }

        private readonly HashSet<string> _allowedLabelNames = new HashSet<string>
        {
            "code",
            "method",
            "controller",
            "action"
        };
        
        private void UpdateMetricValueIfExists(string key, string value)
        {
            if (_labelData.ContainsKey(key)) _labelData[key] = value;
        }
    }
}