using System;
using System.Globalization;
using System.IO;

namespace Prometheus
{
    // The use of BufferedStream here is a bit theoretical from a benefit perspective.
    // Profiling provided contradictory results (more memory use with less buffering??).
    // Revisit if you can come up with more accurate test cases.
    internal sealed class TextSerializer : IMetricsSerializer, IDisposable
    {
        private const byte NewLine = (byte)'\n';
        private const byte Space = (byte)' ';

        public TextSerializer(Stream stream, bool leaveOpen)
        {
            _stream = new Lazy<BufferedStream>(() => new BufferedStream(stream, 16 * 1024));
            _leaveOpen = leaveOpen;
        }

        // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
        public TextSerializer(Func<Stream> streamFactory, bool leaveOpen)
        {
            _stream = new Lazy<BufferedStream>(() => new BufferedStream(streamFactory(), 16 * 1024));
            _leaveOpen = leaveOpen;
        }

        public void Dispose()
        {
            // If we never opened the stream, we don't touch it on close.
            if (_stream.IsValueCreated)
            {
                if (_leaveOpen)
                    _stream.Value.Flush();
                else
                    _stream.Value.Close();
            }
        }

        private readonly Lazy<BufferedStream> _stream;
        private readonly bool _leaveOpen;

        // # HELP name help
        // # TYPE name type
        public void WriteFamilyDeclaration(byte[][] headerLines)
        {
            foreach (var line in headerLines)
            {
                _stream.Value.Write(line, 0, line.Length);
                _stream.Value.WriteByte(NewLine);
            }
        }

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];

        // name{labelkey1="labelvalue1",labelkey2="labelvalue2"} 123.456
        public void WriteMetric(byte[] identifier, double value)
        {
            _stream.Value.Write(identifier, 0, identifier.Length);
            _stream.Value.WriteByte(Space);

            var valueAsString = value.ToString(CultureInfo.InvariantCulture);

            var numBytes = PrometheusConstants.ExportEncoding
                .GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);

            _stream.Value.Write(_stringBytesBuffer, 0, numBytes);
            _stream.Value.WriteByte(NewLine);
        }
    }
}
