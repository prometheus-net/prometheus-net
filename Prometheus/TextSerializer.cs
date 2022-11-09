using System.Globalization;

namespace Prometheus
{
    /// <remarks>
    /// Does NOT take ownership of the stream - caller remains the boss.
    /// </remarks>
    internal sealed class TextSerializer : IMetricsSerializer
    {
        private static readonly byte[] NewLine = new[] { (byte)'\n' };
        private static readonly byte[] Space = new[] { (byte)' ' };

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
        
        /// <summary>
        /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
        /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
        /// </summary>
        public byte[] CreateIdentifier(ChildBase self, string? postfix = null, string? extraLabelName = null, string? extraLabelValue = null)
        {
            // TODO
            //   This function should be reworked to write the identity out to the stream directly, this class will likely
            //   have a StringBuilder to offset the cost of removing the memoization.
            var fullName = postfix != null ? $"{self._parent.Name}_{postfix}" : self._parent.Name;

            var labels = self.FlattenedLabels;

            if (extraLabelName != null && extraLabelValue != null)
            {
                var extraLabelNames = StringSequence.From(extraLabelName);
                var extraLabelValues = StringSequence.From(extraLabelValue);

                var extraLabels = LabelSequence.From(extraLabelNames, extraLabelValues);

                // Extra labels go to the end (i.e. they are deepest to inherit from).
                labels = labels.Concat(extraLabels);
            }

            if (labels.Length != 0)
                return PrometheusConstants.ExportEncoding.GetBytes($"{fullName}{{{labels.Serialize()}}}");
            else
                return PrometheusConstants.ExportEncoding.GetBytes(fullName);
        }
    }
}
