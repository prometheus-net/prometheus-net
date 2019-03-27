using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <remarks>
    /// The use of BufferedStream here is a bit theoretical from a benefit perspective.
    /// Profiling provided contradictory results (more memory use with less buffering??).
    /// Revisit if you can come up with more accurate test cases. So far the results are in favor of BufferedStream:
    /// With BufferedStream:
    /// | CollectAndSerialize | 63.85 ms | 2.528 ms | 7.131 ms | 61.67 ms |   4833.3333 |           - |           - |             7.48 MB |
    /// Without BufferedStream:
    /// | CollectAndSerialize | 50.44 ms | 3.041 ms | 4.263 ms |  13454.5455 |           - |           - |            20.31 MB |
    /// </remarks>
    internal sealed class TextSerializer : IMetricsSerializer
    {
        private static readonly byte[] NewLine = new[] { (byte)'\n' };
        private static readonly byte[] Space = new[] { (byte)' ' };

        public TextSerializer(Stream stream)
        {
            _stream = new Lazy<BufferedStream>(() => new BufferedStream(stream, 16 * 1024));
        }

        // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
        public TextSerializer(Func<Stream> streamFactory)
        {
            _stream = new Lazy<BufferedStream>(() => new BufferedStream(streamFactory(), 16 * 1024));
        }

        public async Task FlushAsync(CancellationToken cancel)
        {
            // If we never opened the stream, we don't touch it on flush.
            if (!_stream.IsValueCreated)
                return;

            await _stream.Value.FlushAsync(cancel);
        }

        private readonly Lazy<BufferedStream> _stream;

        // # HELP name help
        // # TYPE name type
        public async Task WriteFamilyDeclarationAsync(byte[][] headerLines, CancellationToken cancel)
        {
            foreach (var line in headerLines)
            {
                await _stream.Value.WriteAsync(line, 0, line.Length, cancel);
                await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
            }
        }

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];

        // name{labelkey1="labelvalue1",labelkey2="labelvalue2"} 123.456
        public async Task WriteMetricAsync(byte[] identifier, double value, CancellationToken cancel)
        {
            await _stream.Value.WriteAsync(identifier, 0, identifier.Length, cancel);
            await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);

            var valueAsString = value.ToString(CultureInfo.InvariantCulture);

            var numBytes = PrometheusConstants.ExportEncoding
                .GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);

            await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);
            await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
        }
    }
}
