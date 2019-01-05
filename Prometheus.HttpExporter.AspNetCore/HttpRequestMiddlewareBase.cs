using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Prometheus.Advanced;

namespace Prometheus.HttpExporter.AspNetCore
{
    public class HttpRequestMiddlewareBase<T> where T : ICollector
    {
        public HttpRequestMiddlewareBase(T collector)
        {
            if (collector == null || !LabelsAreValid(collector)) throw new ArgumentException(nameof(collector)); 
            _labelNames = collector.LabelNames;
            _labelData = _labelNames.ToDictionary(key => key);
            _requiresRouteData = _labelData.ContainsKey(HttpRequestLabelNames.Action) ||
                                      _labelData.ContainsKey(HttpRequestLabelNames.Controller);
        }

        protected string[] GetLabelData(HttpContext context)
        {
            if (_labelNames.Length == 0) return new string[0];

            if (_requiresRouteData)
            {
                var routeData = context.GetRouteData();

                UpdateMetricValueIfExists(HttpRequestLabelNames.Method, context.Request.Method);
                UpdateMetricValueIfExists(HttpRequestLabelNames.Code, context.Response.StatusCode.ToString());
                UpdateMetricValueIfExists(HttpRequestLabelNames.Action, routeData?.Values["Action"] as string ?? string.Empty);
                UpdateMetricValueIfExists(HttpRequestLabelNames.Controller, routeData?.Values["Controller"] as string ?? string.Empty);
            }
            else
            {
                UpdateMetricValueIfExists(HttpRequestLabelNames.Method, context.Request.Method);
                UpdateMetricValueIfExists(HttpRequestLabelNames.Code, context.Response.StatusCode.ToString());
            }
            
            return _labelNames.Where(_labelData.ContainsKey).Select(x => _labelData[x]).ToArray();
        }

        private bool LabelsAreValid(T counter) => _allowedLabelNames.IsSupersetOf(counter.LabelNames);

        private readonly HashSet<string> _allowedLabelNames = new HashSet<string>
        {
            HttpRequestLabelNames.Code,
            HttpRequestLabelNames.Method,
            HttpRequestLabelNames.Controller,
            HttpRequestLabelNames.Action
        };

        private void UpdateMetricValueIfExists(string key, string value)
        {
            if (_labelData.ContainsKey(key)) _labelData[key] = value;
        }

        private readonly Dictionary<string, string> _labelData;
        private readonly string[] _labelNames;
        private readonly bool _requiresRouteData;
    }
}