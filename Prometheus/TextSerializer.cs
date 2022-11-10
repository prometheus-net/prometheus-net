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
        private static readonly byte[] LBrace = { (byte)'{' };
        private static readonly byte[] RBraceSp = { (byte)'}', (byte)' ' };
        private static readonly byte[] Sp = { (byte)' ' };
        private static readonly byte[] PInf = PrometheusConstants.ExportEncoding.GetBytes("+Inf");

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

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];

        // 123.456
        // Note: Terminates with a NEWLINE
        public async Task WriteValuePartAsync(double value, CancellationToken cancel)
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
        public async Task WriteIdentifierPartAsync(byte[] name, byte[] flatennedLabels, CancellationToken cancel,
            byte[]? postfix = null, byte[]? extraLabelName = null, byte[]? extraLabelValue = null,
            byte[]? extraLabelValueOpenMetrics = null)
        {
            await _stream.Value.WriteAsync(name, 0, name.Length, cancel);
            if (postfix != null && postfix.Length > 0)
            {
                await _stream.Value.WriteAsync(Underscore, 0, Underscore.Length, cancel);
                await _stream.Value.WriteAsync(postfix, 0, postfix.Length, cancel);
            }

            if (flatennedLabels.Length > 0 || (extraLabelName != null && extraLabelValue != null))
            {
                await _stream.Value.WriteAsync(LBrace, 0, LBrace.Length, cancel);
                if (flatennedLabels.Length > 0)
                {
                    await _stream.Value.WriteAsync(flatennedLabels, 0, flatennedLabels.Length, cancel);
                }

                if (extraLabelName != null && extraLabelValue != null)
                {
                    if (flatennedLabels.Length > 0)
                    {
                        await _stream.Value.WriteAsync(Comma, 0, Comma.Length, cancel);
                    }

                    await _stream.Value.WriteAsync(extraLabelName, 0, extraLabelName.Length, cancel);
                    await _stream.Value.WriteAsync(Equal, 0, Equal.Length, cancel);
                    await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
                    await _stream.Value.WriteAsync(extraLabelValue, 0, extraLabelValue.Length, cancel);
                    await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
                }
                await _stream.Value.WriteAsync(RBraceSp, 0, RBraceSp.Length, cancel);
            }
            else
            {
                await _stream.Value.WriteAsync(Sp, 0, Sp.Length, cancel);
            }
        }

        /// <summary>
        /// Encode the system variable in regular Prometheus form and also return a OpenMetrics variant, these can be
        /// the same.
        /// </summary>
        internal static Tuple<byte[], byte[]> EncodeSystemLabelValue(double value)
        {
            if (double.IsPositiveInfinity(value))
            {
                return Tuple.Create(PInf, PInf);
            }

            var valueAsString = value.ToString(CultureInfo.InvariantCulture);
            var bytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString);
            return Tuple.Create(bytes, bytes);
        }
    }
}