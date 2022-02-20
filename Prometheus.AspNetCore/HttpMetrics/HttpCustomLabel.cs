using Microsoft.AspNetCore.Http;
using System;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpCustomLabel
    {
        /// <summary>
        /// Name of the Prometheus label.
        /// </summary>
        public string LabelName { get; }

        /// <summary>
        /// A method that extracts the label value from the HttpContext of the request being handled.
        /// </summary>
        public Func<HttpContext, string> LabelValueProvider { get; }

        public HttpCustomLabel(string labelName, Func<HttpContext, string> labelValueProvider)
        {
            LabelName = labelName;
            LabelValueProvider = labelValueProvider;
        }
    }
}
