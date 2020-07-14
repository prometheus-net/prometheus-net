using System.Linq;
using System.Net.Http;

namespace Prometheus.HttpClientMetrics
{
    /// <summary>
    ///     This base class performs the data management necessary to associate the correct labels and values
    ///     with HttpClient metrics, depending on the options the user has provided for the HttpClient metric handler.
    ///     The following labels are supported:
    ///     'method' (HTTP request method)
    ///     'host' (The host name of  HTTP request)
    ///     'path' (The query path HTTP request)
    /// </summary>
    public abstract class HttpClientDelegatingHandlerBase<TCollector, TChild> : DelegatingHandler
        where TCollector : class, ICollector<TChild>
        where TChild : class, ICollectorChild
    {
        private readonly TCollector _metric;

        protected HttpClientDelegatingHandlerBase(HttpClientMetricsOptionsBase? options,
                                                  TCollector? customMetric)
        {
            MetricFactory = Metrics.WithCustomRegistry(options?.Registry ?? Metrics.DefaultRegistry);

            _metric = customMetric ?? CreateMetricInstance(HttpClientRequestLabelNames.All);
        }

        protected HttpClientDelegatingHandlerBase(HttpMessageHandler innerHandler,
                                                  HttpClientMetricsOptionsBase? options,
                                                  TCollector? customMetric)
        {
            MetricFactory = Metrics.WithCustomRegistry(options?.Registry ?? Metrics.DefaultRegistry);

            _metric = customMetric ?? CreateMetricInstance(HttpClientRequestLabelNames.All);

            InnerHandler = innerHandler;
        }


        /// <summary>
        ///     The factory to use for creating the default metric for this handler.
        /// </summary>
        protected MetricFactory MetricFactory { get; }

        /// <summary>
        ///     Creates the default metric instance with the specified set of labels.
        /// </summary>
        protected abstract TCollector CreateMetricInstance(string[] labelNames);

        /// <summary>
        ///     Creates the metric child instance to use for measurements.
        /// </summary>
        /// <remarks>
        ///     Internal for testing purposes.
        /// </remarks>
        protected internal TChild CreateChild(HttpRequestMessage request)
        {
            if (!_metric.LabelNames.Any())
            {
                return _metric.Unlabelled;
            }


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
                }
            }

            return _metric.WithLabels(labelValues);
        }
    }
}