using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.HttpMetrics
{
    /// <summary>
    /// This class handles getting the data about the current HTTP request to use as label data for the metric
    /// the http request middleware is using.
    /// The metric used may have up to four labels (or none), which must be from the following:
    /// 'code' (HTTP status code)
    /// 'method' (HTTP request method)
    /// 'controller' (The Controller used to fulfill the HTTP request)
    /// 'action' (The Action used to fulfill the HTTP request)
    /// The 'code' and 'method' data are taken from the current HTTP context.
    /// Similarly, if either 'controller' or 'action' is provided, the data will be taken from the RouteData of
    /// the current HTTP context.
    /// </summary>
    /// <typeparam name="T">The metric being used.</typeparam>
    public abstract class HttpRequestMiddlewareBase<T> where T : Collector
    {
        private readonly HashSet<string> _allowedLabelNames = new HashSet<string>(HttpRequestLabelNames.All);

        private readonly Dictionary<string, string> _labelData;
        private readonly string[] _labelNames;
        private readonly bool _requiresRouteData;

        protected HttpRequestMiddlewareBase(T collector)
        {
            if (collector == null) throw new ArgumentException(nameof(collector));

            if (!LabelsAreValid(collector.LabelNames))
                throw new ArgumentException(
                    $"The metric used for HTTP requests may only use labels from the following set: {string.Join(", ", HttpRequestLabelNames.All)}");

            _labelNames = collector.LabelNames;
            _labelData = _labelNames.ToDictionary(key => key);
            _requiresRouteData = _labelData.ContainsKey(HttpRequestLabelNames.Action) ||
                                 _labelData.ContainsKey(HttpRequestLabelNames.Controller);
        }

        protected string[] GetLabelData(HttpContext context)
        {
            if (_labelNames.Length == 0) return new string[0];

            UpdateMetricValueIfExists(HttpRequestLabelNames.Method, context.Request.Method);
            UpdateMetricValueIfExists(HttpRequestLabelNames.Code, context.Response.StatusCode.ToString());

            if (_requiresRouteData)
            {
                var routeData = context.GetRouteData();

                UpdateMetricValueIfExists(HttpRequestLabelNames.Action,
                    routeData?.Values["Action"] as string ?? string.Empty);
                UpdateMetricValueIfExists(HttpRequestLabelNames.Controller,
                    routeData?.Values["Controller"] as string ?? string.Empty);
                if (routeData != null && routeData.Values.ContainsKey("odataPath"))
                    HandleODataPath(routeData);
                
            }

            return _labelNames.Where(_labelData.ContainsKey).Select(x => _labelData[x]).ToArray();
        }

        private void HandleODataPath(RouteData routeData)
        {
            var controller = routeData.Values["odataPath"] as string ?? string.Empty;
            //Remove the parameters from the odataPath to avoid getting too many entry points - Example Item('A00001') -> Item() | CopyFromItem(ItemKey='X') -> CopyFromItem(ItemKey=) | Transfer(Number=1) -> Transfer(Number=) 
            foreach (var x in routeData.Values.Where(x => x.Key != "odataPath" && x.Key != "action").OrderByDescending(x => (x.Value as string ?? string.Empty).Length))
            {
                var value = x.Value as string ?? string.Empty;
                if (value.Length > 0)
                {
                    controller = controller.Replace(value, string.Empty);
                }
            }

            UpdateMetricValueIfExists(HttpRequestLabelNames.Controller, controller.Replace("'", string.Empty));
        }

        private bool LabelsAreValid(string[] labelNames)
        {
            return _allowedLabelNames.IsSupersetOf(labelNames);
        }

        private void UpdateMetricValueIfExists(string key, string value)
        {
            if (_labelData.ContainsKey(key)) _labelData[key] = value;
        }
    }
}