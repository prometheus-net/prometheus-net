using System.Net.Http.Headers;
using System.Text;

namespace Prometheus;

public static class PrometheusConstants
{
    public const string TextContentType = "text/plain";
    public const string OpenMetricsContentType = "application/openmetrics-text";

    public const string TextContentTypeWithVersionAndEncoding = TextContentType + "; version=0.0.4; charset=utf-8";
    public const string OpenMetricsContentTypeWithVersionAndEncoding = OpenMetricsContentType + "; version=1.0.0; charset=utf-8";

    // ASP.NET requires a MediaTypeHeaderValue object
    public static readonly MediaTypeHeaderValue ExporterContentTypeValue = MediaTypeHeaderValue.Parse(TextContentTypeWithVersionAndEncoding);
    public static readonly MediaTypeHeaderValue ExporterOpenMetricsContentTypeValue = MediaTypeHeaderValue.Parse(OpenMetricsContentTypeWithVersionAndEncoding);

    // Use UTF-8 encoding, but provide the flag to ensure the Unicode Byte Order Mark is never prepended to the output stream.
    public static readonly Encoding ExportEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal static readonly Encoding ExemplarEncoding = new ASCIIEncoding();
}