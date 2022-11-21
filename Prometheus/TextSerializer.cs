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
        private static readonly byte[] SpaceHashSpaceLeftBrace = { (byte)' ', (byte)'#', (byte)' ', (byte)'{' };
        private static readonly byte[] PositiveInfinity = PrometheusConstants.ExportEncoding.GetBytes("+Inf");
        private static readonly byte[] NegativeInfinity = PrometheusConstants.ExportEncoding.GetBytes("-Inf");
        private static readonly byte[] NotANumber = PrometheusConstants.ExportEncoding.GetBytes("NaN");
        private static readonly byte[] PositiveOne = PrometheusConstants.ExportEncoding.GetBytes("1.0");
        private static readonly byte[] Zero = PrometheusConstants.ExportEncoding.GetBytes("0.0");
        private static readonly byte[] NegativeOne = PrometheusConstants.ExportEncoding.GetBytes("-1.0");
        private static readonly byte[] EofNewLine = PrometheusConstants.ExportEncoding.GetBytes("# EOF\n");
        private static readonly byte[] HashHelpSpace = PrometheusConstants.ExportEncoding.GetBytes("# HELP ");
        private static readonly byte[] NewlineHashTypeSpace = PrometheusConstants.ExportEncoding.GetBytes("\n# TYPE ");
        private static readonly byte[] Unknown = PrometheusConstants.ExportEncoding.GetBytes("unknown");

        public TextSerializer(Stream stream, ExpositionFormat fmt = ExpositionFormat.Text)
        {
            _fmt = fmt;
            _stream = new Lazy<Stream>(() => stream);
        }

        // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
        public TextSerializer(Func<Stream> streamFactory,
            ExpositionFormat fmt = ExpositionFormat.Text)
        {
            _fmt = fmt;
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

        public async Task WriteFamilyDeclarationAsync(string name, byte[]nameBytes, byte[] helpBytes, MetricType type, 
            byte[] typeBytes, CancellationToken cancel)
        {
            var nameLen = nameBytes.Length; 
            if (_fmt == ExpositionFormat.OpenMetricsText && type == MetricType.Counter)
            {
                if (name.EndsWith("_total"))
                {
                    nameLen -= 6; // in OpenMetrics the counter name does not include the _total prefix.
                }
                else
                {
                    typeBytes = Unknown; // if the total prefix is missing the _total prefix it is out of spec
                }
            }

            await _stream.Value.WriteAsync(HashHelpSpace, 0, HashHelpSpace.Length, cancel);
            await _stream.Value.WriteAsync(nameBytes, 0, nameLen, cancel);
            if (helpBytes.Length > 0)
            {
                await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
                await _stream.Value.WriteAsync(helpBytes, 0, helpBytes.Length, cancel);
            }
            await _stream.Value.WriteAsync(NewlineHashTypeSpace, 0, NewlineHashTypeSpace.Length, cancel);
            await _stream.Value.WriteAsync(nameBytes, 0, nameLen, cancel);
            await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
            await _stream.Value.WriteAsync(typeBytes, 0, typeBytes.Length, cancel);
            await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
        }

        public async Task WriteEnd(CancellationToken cancel)
        {
            if (_fmt == ExpositionFormat.OpenMetricsText)
                await _stream.Value.WriteAsync(EofNewLine, 0, EofNewLine.Length, cancel);
        }

        public async Task WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
            CancellationToken cancel, double value, ObservedExemplar exemplar, byte[]? suffix = null)
        {
            await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, exemplar, suffix);
            await WriteValuePartAsync(value, exemplar, cancel);
        }

        private async Task WriteExemplarAsync(CancellationToken cancel, ObservedExemplar exemplar)
        {
            await _stream.Value.WriteAsync(SpaceHashSpaceLeftBrace, 0, SpaceHashSpaceLeftBrace.Length, cancel);
            for (var i = 0; i < exemplar.Labels!.Length; i++)
            {
                if (i > 0)
                    await _stream.Value.WriteAsync(Comma, 0, Comma.Length, cancel);
                await WriteLabel(exemplar.Labels[i].KeyBytes, exemplar.Labels[i].ValueBytes, cancel);
            }

            await _stream.Value.WriteAsync(RightBraceSpace, 0, RightBraceSpace.Length, cancel);
            await WriteValue(exemplar.Val, cancel);
            await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
            await WriteValue(exemplar.Timestamp, cancel);
        }

        private async Task WriteLabel(byte[] label, byte[] value, CancellationToken cancel)
        {
            await _stream.Value.WriteAsync(label, 0, label.Length, cancel);
            await _stream.Value.WriteAsync(Equal, 0, Equal.Length, cancel);
            await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
            await _stream.Value.WriteAsync(value, 0, value.Length, cancel);
            await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
        }

        private async Task WriteValue(double value, CancellationToken cancel)
        {
            if (_fmt == ExpositionFormat.OpenMetricsText)
            {
                switch (value)
                {
                    case 0:
                        await _stream.Value.WriteAsync(Zero, 0, Zero.Length, cancel);
                        return;
                    case 1:
                        await _stream.Value.WriteAsync(PositiveOne, 0, PositiveOne.Length, cancel);
                        return;
                    case -1:
                        await _stream.Value.WriteAsync(NegativeOne, 0, NegativeOne.Length, cancel);
                        return;
                    case double.PositiveInfinity:
                        await _stream.Value.WriteAsync(PositiveInfinity, 0, PositiveInfinity.Length, cancel);
                        return;
                    case double.NegativeInfinity:
                        await _stream.Value.WriteAsync(NegativeInfinity, 0, NegativeInfinity.Length, cancel);
                        return;
                    case double.NaN:
                        await _stream.Value.WriteAsync(NotANumber, 0, NotANumber.Length, cancel);
                        return;
                }
            }

            var valueAsString = value.ToString(CultureInfo.InvariantCulture);
            if (_fmt == ExpositionFormat.OpenMetricsText && !valueAsString.Contains("."))
                valueAsString += ".0";
            var numBytes = PrometheusConstants.ExportEncoding
                .GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);
            await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);
        }

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];
        private readonly ExpositionFormat _fmt;

        // 123.456
        // Note: Terminates with a NEWLINE
        private async Task WriteValuePartAsync(double value, ObservedExemplar exemplar, CancellationToken cancel)
        {
            await WriteValue(value, cancel);
            if (_fmt == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
            {
                await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
                await WriteExemplarAsync(cancel, exemplar);
            }

            await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
        }
        

        /// <summary>
        /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
        /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
        /// Note: Terminates with a SPACE
        /// </summary>
        private async Task WriteIdentifierPartAsync(byte[] name, byte[] flattenedLabels, CancellationToken cancel,
            CanonicalLabel canonicalLabel, ObservedExemplar observedExemplar, byte[]? suffix = null)
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
                    if (_fmt == ExpositionFormat.OpenMetricsText)
                        await _stream.Value.WriteAsync(
                            canonicalLabel.OpenMetrics, 0, canonicalLabel.OpenMetrics.Length, cancel);
                    else
                        await _stream.Value.WriteAsync(
                            canonicalLabel.Prometheus, 0, canonicalLabel.Prometheus.Length, cancel);
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
            var prom = PrometheusConstants.ExportEncoding.GetBytes(valueAsString);

            return new CanonicalLabel(name, prom, valueAsString.Contains(".")
                ? prom
                : PrometheusConstants.ExportEncoding.GetBytes(valueAsString + ".0"));
        }
    }
}