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

            Stream oldStream = await response.Content.ReadAsStreamAsync();

            Wrap(response, oldStream, delegate
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

        private void Wrap(HttpResponseMessage response, Stream oldStream, Action onEndOfStream)
        {
            var newContent = new StreamContent(new EndOfStreamDetectingStream(oldStream, onEndOfStream));

            var oldHeaders = response.Content.Headers;
            var newHeaders = newContent.Headers;

#if NET6_0_OR_GREATER
        foreach (KeyValuePair<string, HeaderStringValues> header in oldHeaders.NonValidated)
        {
            if (header.Value.Count > 1)
            {
                newHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
            else
            {
                newHeaders.TryAddWithoutValidation(header.Key, header.Value.ToString());
            }
        }
#else
            foreach (var header in oldHeaders)
            {
                newHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
#endif

            response.Content = newContent;
        }

        private sealed class EndOfStreamDetectingStream : Stream
        {
            public EndOfStreamDetectingStream(Stream inner, Action onEndOfStream)
            {
                _inner = inner;
                _onEndOfStream = onEndOfStream;
            }

            private readonly Stream _inner;
            private readonly Action _onEndOfStream;
            private int _sawEndOfStream = 0;

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _inner.Read(buffer, offset, count);

                if (read == 0 && buffer.Length != 0)
                {
                    SignalCompletion();
                }

                return read;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return buffer.Length == 0
                    ? _inner.ReadAsync(buffer, offset, count, cancellationToken)
                    : ReadAsyncCore(this, _inner.ReadAsync(buffer, offset, count, cancellationToken));

                static async Task<int> ReadAsyncCore(EndOfStreamDetectingStream stream, Task<int> readTask)
                {
                    int read = await readTask;

                    if (read == 0)
                    {
                        stream.SignalCompletion();
                    }

                    return read;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    SignalCompletion();

                    _inner.Dispose();
                }
            }

            private void SignalCompletion()
            {
                if (Interlocked.Exchange(ref _sawEndOfStream, 1) == 0)
                {
                    _onEndOfStream();
                }
            }

            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
        }
    }
}