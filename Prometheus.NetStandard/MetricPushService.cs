using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Prometheus.Advanced.DataContracts;

namespace Prometheus
{
    public class MetricPushService : IMetricPushService
    {
        private readonly HttpClient _httpClient;

        public const string ContentType = "text/plain; version=0.0.4";

        protected virtual HttpMessageHandler MessageHandler => new HttpClientHandler();

        public MetricPushService()
        {
            _httpClient = new HttpClient(MessageHandler);
        }

        /// <summary>
        /// Push metrics to single pushgateway endpoint
        /// </summary>
        /// <param name="metricFamilies">Collection of metrics</param>
        /// <param name="endpoint">PushGateway endpoint</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance name</param>
        /// <param name="contentType">content-type</param>
        /// <param name="additionalLabels">additional labels</param>
        /// <returns></returns>
        public async Task PushAsync(IEnumerable<MetricFamily> metricFamilies, string endpoint, string job, string instance, string contentType = ContentType, IEnumerable<Tuple<string, string>> additionalLabels = null)
        {
            await PushAsync(metricFamilies, new[] { endpoint }, job, instance, contentType, additionalLabels).ConfigureAwait(false);
        }

        /// <summary>
        /// Push metrics to single pushgateway endpoint
        /// </summary>
        /// <param name="metrics">Collection of metrics</param>
        /// <param name="endpoints">PushGateway endpoints - fault-tolerance</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance name</param>
        /// <param name="contentType">content-type</param>
        /// <param name="additionalLabels">additional labels</param>
        /// <returns></returns>
        public async Task PushAsync(IEnumerable<MetricFamily> metrics, string[] endpoints, string job, string instance, string contentType = ContentType, IEnumerable<Tuple<string, string>> additionalLabels = null)
        {
            if (endpoints == null || !endpoints.Any())
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            var tasks = new List<Task<HttpResponseMessage>>(endpoints.Length);
            var streamsToDispose = new List<Stream>();

            foreach (var endpoint in endpoints)
            {
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException(nameof(endpoint));
                }

                var url = $"{endpoint.TrimEnd('/')}/metrics/job/{job}";
                if (!string.IsNullOrEmpty(instance))
                {
                    url = $"{url}/instance/{instance}";
                }

                var sb = new StringBuilder();
                sb.Append(url);
                if (additionalLabels != null)
                {
                    foreach (var pair in additionalLabels)
                    {
                        if (pair == null || string.IsNullOrEmpty(pair.Item1) || string.IsNullOrEmpty(pair.Item2))
                        {
                            // TODO: Surely this should throw an exception?
                            Trace.WriteLine("Ignoring invalid label set");
                            continue;
                        }

                        sb.AppendFormat("/{0}/{1}", pair.Item1, pair.Item2);
                    }
                }

                if (!Uri.TryCreate(sb.ToString(), UriKind.Absolute, out var targetUrl))
                {
                    throw new ArgumentException("Endpoint must be a valid url", nameof(endpoint));
                }

                var memoryStream = new MemoryStream();
                streamsToDispose.Add(memoryStream);
                ScrapeHandler.ProcessScrapeRequest(metrics, contentType, memoryStream);
                memoryStream.Position = 0;

                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException(nameof(endpoint));
                }

                var streamContent = new StreamContent(memoryStream);
                tasks.Add(_httpClient.PostAsync(targetUrl, streamContent));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            Exception exception = null;
            foreach (var task in tasks)
            {
                var response = await task.ConfigureAwait(false);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }

            streamsToDispose.ForEach(s => s.Dispose());

            if (exception != null)
            {
                throw exception;
            }
        }
    }

    public interface IMetricPushService
    {
        /// <summary>
        /// Push metrics to single pushgateway endpoint
        /// </summary>
        /// <param name="metricFamilies">Collection of metrics</param>
        /// <param name="endpoint">PushGateway endpoint</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance name</param>
        /// <param name="contentType">content-type</param>
        /// <param name="additionalLabels">additional labels</param>
        /// <returns></returns>
        Task PushAsync(IEnumerable<MetricFamily> metricFamilies, string endpoint, string job, string instance,
            string contentType = MetricPushService.ContentType, IEnumerable<Tuple<string, string>> additionalLabels = null);

        /// <summary>
        /// Push metrics to single pushgateway endpoint
        /// </summary>
        /// <param name="metrics">Collection of metrics</param>
        /// <param name="endpoints">PushGateway endpoints - fault-tolerance</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance name</param>
        /// <param name="contentType">content-type</param>
        /// <param name="additionalLabels">additional labels</param>
        /// <returns></returns>
        Task PushAsync(IEnumerable<MetricFamily> metrics, string[] endpoints, string job, string instance,
            string contentType = MetricPushService.ContentType, IEnumerable<Tuple<string, string>> additionalLabels = null);
    }
}
