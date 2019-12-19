using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Globalization;
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
    public abstract class HttpRequestMiddlewareBase<TCollector, TChild>
        where TCollector : ICollector<TChild>
        where TChild : ICollectorChild
    {
        protected abstract string[] AllowedLabelNames { get; }

        /// <summary>
        /// Informs the label data logic on whether we need to account for routing data in the metric labels.
        /// Can also be used by subclasses to determine if they need to export routing-specific logic.
        /// </summary>
        protected readonly bool _labelsIncludeRouteData;

        protected readonly TCollector _metric;

        protected HttpRequestMiddlewareBase(TCollector metric)
        {
            if (metric == null) throw new ArgumentException(nameof(metric));

            if (!LabelsAreValid(metric.LabelNames))
                throw new ArgumentException($"{metric.Name} may only use labels from the following set: {string.Join(", ", AllowedLabelNames)}");

            _labelsIncludeRouteData = metric.LabelNames.Intersect(HttpRequestLabelNames.RouteSpecific).Any();
            _metric = metric;
        }

        protected TChild CreateChild(HttpContext context)
        {
            if (!_metric.LabelNames.Any())
                return _metric.Unlabelled;

            if (!_labelsIncludeRouteData)
                return CreateChild(context, null);

            var routeData = context.Features.Get<ICapturedRouteDataFeature>()?.Values;

            // If we have captured route data, we always prefer it.
            // Otherwise, we extract new route data right now.
            if (routeData == null)
                routeData = context.GetRouteData()?.Values;

            return CreateChild(context, routeData);
        }

        protected TChild CreateChild(HttpContext context, RouteValueDictionary? routeData)
        {
            var labelValues = new string[_metric.LabelNames.Length];

            for (var i = 0; i < labelValues.Length; i++)
            {
                switch (_metric.LabelNames[i])
                {
                    case HttpRequestLabelNames.Method:
                        labelValues[i] = context.Request.Method;
                        break;
                    case HttpRequestLabelNames.Code:
                        labelValues[i] = context.Response.StatusCode.ToString(CultureInfo.InvariantCulture);
                        break;
                    case HttpRequestLabelNames.Controller:
                        labelValues[i] = routeData?["Controller"] as string ?? string.Empty;
                        break;
                    case HttpRequestLabelNames.Action:
                        labelValues[i] = routeData?["Action"] as string ?? string.Empty;
                        break;
                    default:
                        throw new NotSupportedException($"Unexpected label name on {_metric.Name}: {_metric.LabelNames[i]}");
                }
            }

            return _metric.WithLabels(labelValues);
        }

        private bool LabelsAreValid(string[] labelNames)
        {
            return !labelNames.Except(AllowedLabelNames).Any();
        }
    }
}