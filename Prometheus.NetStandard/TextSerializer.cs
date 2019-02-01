using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Prometheus
{
    internal sealed class TextSerializer : IMetricsSerializer, IDisposable
    {
        // Use UTF-8 encoding, but provide the flag to ensure the Unicode Byte Order Mark is never
        // pre-pended to the output stream.
        private static readonly Encoding Encoding = new UTF8Encoding(false);

        public TextSerializer(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _writer = new StreamWriter(stream, Encoding, 16 * 1024, leaveOpen: true);
            _writer.NewLine = "\n";
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        private readonly StreamWriter _writer;

        // HELP name help
        // TYPE name type
        public void WriteFamilyDeclaration(string[] headerLines)
        {
            foreach (var line in headerLines)
                _writer.WriteLine(line);
        }

        // name{labelkey1="labelvalue1",labelkey2="labelvalue2"} 123.456
        public void WriteMetric(string identifier, double value)
        {
            _writer.Write(identifier);
            _writer.Write(' ');
            _writer.WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
