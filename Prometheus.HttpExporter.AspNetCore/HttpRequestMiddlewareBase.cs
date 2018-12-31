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
            if (collector == null || !this.LabelsAreValid(collector)) throw new ArgumentException(nameof(collector)); 
            this._labelNames = collector.LabelNames;
            this._labelData = this._labelNames.ToDictionary(key => key);
            this._requiresRouteData = this._labelData.ContainsKey(LabelNames.Action) ||
                                      this._labelData.ContainsKey(LabelNames.Controller);
        }

        protected string[] GetLabelData(HttpContext context)
        {
            if (this._labelNames.Length == 0)
            {
                return new string[0];
            }

            if (this._requiresRouteData)
            {
                var routeData = context.GetRouteData();

                this.UpdateMetricValueIfExists(LabelNames.Method, context.Request.Method);
                this.UpdateMetricValueIfExists(LabelNames.Code, context.Response.StatusCode.ToString());
                this.UpdateMetricValueIfExists(LabelNames.Action, routeData?.Values["Action"] as string ?? string.Empty);
                this.UpdateMetricValueIfExists(LabelNames.Controller, routeData?.Values["Controller"] as string ?? string.Empty);
            }
            else
            {
                this.UpdateMetricValueIfExists(LabelNames.Method, context.Request.Method);
                this.UpdateMetricValueIfExists(LabelNames.Code, context.Response.StatusCode.ToString());
            }
            
            return this._labelNames.Where(this._labelData.ContainsKey).Select(x => this._labelData[x]).ToArray();
        }

        private bool LabelsAreValid(T counter)
        {
            if (!this._allowedLabelNames.IsSupersetOf(counter.LabelNames)) return false;

            return true;
        }

        private readonly HashSet<string> _allowedLabelNames = new HashSet<string>
        {
            LabelNames.Code,
            LabelNames.Method,
            LabelNames.Controller,
            LabelNames.Action
        };

        private void UpdateMetricValueIfExists(string key, string value)
        {
            if (this._labelData.ContainsKey(key)) this._labelData[key] = value;
        }

        private readonly Dictionary<string, string> _labelData;
        private readonly string[] _labelNames;
        private bool _requiresRouteData;

        private static class LabelNames
        {
            public const string Code = "code";
            public const string Method = "method";
            public const string Controller = "controller";
            public const string Action = "action";
        }
    }
}