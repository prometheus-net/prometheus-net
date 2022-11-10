using System.Globalization;
using System.Text;

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

        private readonly StringBuilder _sb = new();

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
        public async Task WriteIdentifierPartAsync(ChildBase metric, CancellationToken cancel, 
            string? postfix = null, string? extraLabelName = null, string? extraLabelValue = null)
        {
            _sb.Clear();
            _sb.Append(metric._parent.Name);
            if(postfix != null)
            {
                _sb.Append("_");
                _sb.Append(postfix);
            }

            _sb.Append("{");
            SerializeLabels(metric.FlattenedLabels);
            if (extraLabelName != null && extraLabelValue != null)
            {
                if (metric.FlattenedLabels.Length > 1) _sb.Append(",");
                SerialiseLabelValue(extraLabelName, extraLabelValue);
            }
            _sb.Append("} "); // <--- note the whitespace
            var bytes = Encoding.UTF8.GetBytes(_sb.ToString());
            await _stream.Value.WriteAsync(bytes, 0, bytes.Length, cancel);
        }
        
        /// <summary>
        /// Serializes to the labelkey1="labelvalue1",labelkey2="labelvalue2" label string.
        /// </summary>
        private void SerializeLabels(LabelSequence labels)
        {
            // Result is cached in child collector - no need to worry about efficiency here.

            var nameEnumerator = labels.Names.GetEnumerator();
            var valueEnumerator = labels.Values.GetEnumerator();

            for (var i = 0; i < labels.Names.Length; i++)
            {
                if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
                if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

                if (i != 0)
                    _sb.Append(',');

                SerialiseLabelValue(nameEnumerator.Current, valueEnumerator.Current);
            }
        }
        
        private void SerialiseLabelValue(String name, String value)
        {
            _sb.Append(name);
            _sb.Append('=');
            _sb.Append('"');
            _sb.Append(EscapeLabelValue(value));
            _sb.Append('"');
        }
        
        private static string EscapeLabelValue(string value)
        {
            
            return value
                .Replace("\\", @"\\")
                .Replace("\n", @"\n")
                .Replace("\"", @"\""");
        }
    }
}
