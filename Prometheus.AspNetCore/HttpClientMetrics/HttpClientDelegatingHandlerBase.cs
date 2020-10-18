using System;
using System.Linq;
using System.Net.Http;

namespace Prometheus.HttpClientMetrics
{
    /// <summary>
    /// This base class performs the data management necessary to associate the correct labels and values
    /// with HttpClient metrics, depending on the options the user has provided for the HttpClient metric handler.
    /// 
    /// The following labels are supported:
    /// 'method' (HTTP request method)
    /// 'host' (The host name of  HTTP request)
    /// </summary>
    internal abstract class HttpClientDelegatingHandlerBase<TCollector, TChild> : DelegatingHandler
        where TCollector : class, ICollector<TChild>
        where TChild : class, ICollectorChild
    {
        /// <summary>
        /// The factory to use for creating the default metric for this middleware.
        /// Not used if a custom metric is already provided in options.
        /// </summary>
        protected MetricFactory MetricFactory { get; }

        /// <summary>
        /// Creates the default metric instance with the specified set of labels.
        /// Only used if the caller does not provide a custom metric instance in the options.
        /// </summary>
        protected abstract TCollector CreateMetricInstance(string[] labelNames);

        // Internal only for tests.
        internal readonly TCollector _metric;

        protected HttpClientDelegatingHandlerBase(HttpClientMetricsOptionsBase? options, TCollector? customMetric)
        {
            MetricFactory = Metrics.WithCustomRegistry(options?.Registry ?? Metrics.DefaultRegistry);

            if (customMetric != null)
            {
                _metric = customMetric;

                ValidateNoUnexpectedLabelNames();
            }
            else
            {
                _metric = CreateMetricInstance(HttpClientRequestLabelNames.All);
            }
        }

        /// <summary>
        /// Creates the metric child instance to use for measurements.
        /// </summary>
        /// <remarks>
        /// Internal for testing purposes.
        /// </remarks>
        protected internal TChild CreateChild(HttpRequestMessage request)
        {
            if (!_metric.LabelNames.Any())
                return _metric.Unlabelled;

            var labelValues = new string[_metric.LabelNames.Length];

            for (var i = 0; i < labelValues.Length; i++)
            {
                switch (_metric.LabelNames[i])
                {
                    case HttpClientRequestLabelNames.Method:
                        labelValues[i] = request.Method.Method;
                        break;
                    case HttpClientRequestLabelNames.Host:
                        labelValues[i] = request.RequestUri.Host;
                        break;
                    default:
                        // We validate the label set on initialization, so this is impossible.
                        throw new NotSupportedException($"Found unsupported label on metric: {_metric.LabelNames[i]}");
                }
            }

            return _metric.WithLabels(labelValues);
        }

        /// <summary>
        /// If we use a custom metric, it should not have labels that are not among the defaults.
        /// </summary>
        private void ValidateNoUnexpectedLabelNames()
        {
            var allowedLabels = HttpClientRequestLabelNames.All;
            var unexpected = _metric.LabelNames.Except(allowedLabels);

            if (unexpected.Any())
                throw new ArgumentException($"Provided custom HttpClient metric instance for {GetType().Name} has some unexpected labels: {string.Join(", ", unexpected)}.");
        }
    }
}