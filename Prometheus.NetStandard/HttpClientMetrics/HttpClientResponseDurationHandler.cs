using System;
using System.IO;
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
            var responseStream = await response.Content.ReadAsStreamAsync();

            var wrapper = new EndOfStreamDetectingStream(responseStream, delegate
            {
                CreateChild(request, response).Observe(stopWatch.GetElapsedTime().TotalSeconds);
            });

            // Replace the Content with an implementation that observes when it has been fully read.
            var oldContent = response.Content;
            response.Content = new StreamContent(wrapper);

            // Copy headers.
            foreach (var header in oldContent.Headers)
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);

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

        private sealed class EndOfStreamDetectingStream : Stream
        {
            public EndOfStreamDetectingStream(Stream inner, Action onEndOfStream)
            {
                _inner = inner;
                _onEndOfStream = onEndOfStream;
            }

            private readonly Stream _inner;
            private readonly Action _onEndOfStream;

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _inner.Read(buffer, offset, count);

                if (bytesRead == 0)
                    SignalCompletion();

                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await base.ReadAsync(buffer, offset, count, cancellationToken);

                if (bytesRead == 0)
                    SignalCompletion();

                return bytesRead;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                SignalCompletion();
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

            private volatile bool _completed;

            private void SignalCompletion()
            {
                if (_completed)
                    return;

                _completed = true;
                _onEndOfStream();
            }
        }
    }
}