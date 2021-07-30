using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    internal sealed class HttpClientResponseDurationHandler : HttpClientDelegatingHandlerBase<ICollector<IHistogram>, IHistogram>
    {
        public HttpClientResponseDurationHandler(HttpClientResponseDurationOptions? options, HttpClientIdentity identity)
            : base(options, options?.Histogram, identity)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopWatch = ValueStopwatch.StartNew();

            var response = await base.SendAsync(request, cancellationToken);

            // Replace the Content with an implementation that observes when it has been read.
            response.Content = new InterceptingHttpContent(response.Content, delegate
            {
                CreateChild(request, response).Observe(stopWatch.GetElapsedTime().TotalSeconds);
            });

            return response;
        }

        protected override string[] DefaultLabels => HttpClientRequestLabelNames.All;

        protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateHistogram(
            "httpclient_response_duration_seconds",
            "Duration histogram of HTTP requests performed by an HttpClient, measuring the duration until the HTTP response finished being processed.",
            new HistogramConfiguration
            {
                // 1 ms to 32K ms buckets
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                LabelNames = labelNames
            });

        // Not quite super-functional but... perhaps good enough?
        private sealed class InterceptingHttpContent : HttpContent
        {
            public InterceptingHttpContent(HttpContent inner, Action onSerialized)
            {
                _inner = inner;
                _onSerialized = onSerialized;

                if (inner != null)
                {
                    foreach (var header in inner.Headers)
                        Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            private readonly HttpContent _inner;
            private readonly Action _onSerialized;

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                if (_inner != null)
                {
                    var innerStream = await _inner.ReadAsStreamAsync();
                    await innerStream.CopyToAsync(stream);
                }

                TriggerCallback();
            }

            protected override bool TryComputeLength(out long length)
            {
                length = default;
                return false;
            }

            private bool _disposed;
            private readonly object _disposedLock = new object();

            protected override void Dispose(bool disposing)
            {
                if (!disposing)
                    return;

                lock (_disposedLock)
                {
                    if (_disposed)
                        return;

                    _disposed = true;
                }

                TriggerCallback();
                _inner?.Dispose();
            }

            private bool _callbackTriggered;
            private readonly object _callbackTriggeredLock = new object();

            private void TriggerCallback()
            {
                lock (_callbackTriggeredLock)
                {
                    if (_callbackTriggered)
                        return;

                    _callbackTriggered = true;
                }

                _onSerialized();
            }
        }
    }
}