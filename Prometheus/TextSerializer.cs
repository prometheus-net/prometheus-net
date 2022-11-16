using System.Globalization;

namespace Prometheus
{
    /// <remarks>
    /// Does NOT take ownership of the stream - caller remains the boss.
    /// </remarks>
    internal sealed class TextSerializer : IMetricsSerializer
    {
        private static readonly byte[] NewLine = { (byte)'\n' };
        private static readonly byte[] Quote = { (byte)'"' };
        private static readonly byte[] Equal = { (byte)'=' };
        private static readonly byte[] Comma = { (byte)',' };
        private static readonly byte[] Underscore = { (byte)'_' };
        private static readonly byte[] LeftBrace = { (byte)'{' };
        private static readonly byte[] RightBraceSpace = { (byte)'}', (byte)' ' };
        private static readonly byte[] Space = { (byte)' ' };
        private static readonly byte[] PositiveInfinity = PrometheusConstants.ExportEncoding.GetBytes("+Inf");

        public TextSerializer(Stream stream)
        {
            _stream = new Lazy<Stream>(() => stream);
        }

        // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
        public TextSerializer(Func<Stream> streamFactory)
        {
            _stream = new Lazy<Stream>(streamFactory);
        }

        public async Task FlushAsync(CancellationToken cancel)
        {
            // If we never opened the stream, we don't touch it on flush.
            if (!_stream.IsValueCreated)
                return;

            await _stream.Value.FlushAsync(cancel);
        }

        private readonly Lazy<Stream> _stream;

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

        public async Task WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
            CancellationToken cancel,
            double value, byte[]? suffix = null)
        {
            await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, suffix);
            await WriteValuePartAsync(value, cancel);
        }

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];

        // 123.456
        // Note: Terminates with a NEWLINE
        private async Task WriteValuePartAsync(double value, CancellationToken cancel)
        {
            var valueAsString = value.ToString(CultureInfo.InvariantCulture);
            var numBytes = PrometheusConstants.ExportEncoding
                .GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);

            await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);
            await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
        }

        /// <summary>
        /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
        /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
        /// Note: Terminates with a SPACE
        /// </summary>
        private async Task WriteIdentifierPartAsync(byte[] name, byte[] flattenedLabels, CancellationToken cancel,
            CanonicalLabel canonicalLabel, byte[]? suffix = null)
        {
            await _stream.Value.WriteAsync(name, 0, name.Length, cancel);
            if (suffix != null && suffix.Length > 0)
            {
                await _stream.Value.WriteAsync(Underscore, 0, Underscore.Length, cancel);
                await _stream.Value.WriteAsync(suffix, 0, suffix.Length, cancel);
            }

            if (flattenedLabels.Length > 0 || canonicalLabel.IsNotEmpty)
            {
                await _stream.Value.WriteAsync(LeftBrace, 0, LeftBrace.Length, cancel);
                if (flattenedLabels.Length > 0)
                {
                    await _stream.Value.WriteAsync(flattenedLabels, 0, flattenedLabels.Length, cancel);
                }

                // Extra labels go to the end (i.e. they are deepest to inherit from).
                if (canonicalLabel.IsNotEmpty)
                {
                    if (flattenedLabels.Length > 0)
                    {
                        await _stream.Value.WriteAsync(Comma, 0, Comma.Length, cancel);
                    }

                    await _stream.Value.WriteAsync(canonicalLabel.Name, 0, canonicalLabel.Name.Length, cancel);
                    await _stream.Value.WriteAsync(Equal, 0, Equal.Length, cancel);
                    await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
                    await _stream.Value.WriteAsync(canonicalLabel.Prometheus, 0, canonicalLabel.Prometheus.Length,
                        cancel);
                    await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
                }

                await _stream.Value.WriteAsync(RightBraceSpace, 0, RightBraceSpace.Length, cancel);
            }
            else
            {
                await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
            }
        }

        /// <summary>
        /// Encode the special variable in regular Prometheus form and also return a OpenMetrics variant, these can be
        /// the same.
        /// see: https://github.com/OpenObservability/OpenMetrics/blob/main/specification/OpenMetrics.md#considerations-canonical-numbers
        /// </summary>
        internal static CanonicalLabel EncodeValueAsCanonicalLabel(byte[] name, double value)
        {
            if (double.IsPositiveInfinity(value))
                return new CanonicalLabel(name, PositiveInfinity, PositiveInfinity);

            var valueAsString = value.ToString(CultureInfo.InvariantCulture);
            var bytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString);
            return new CanonicalLabel(name, bytes, bytes);
        }
    }
}