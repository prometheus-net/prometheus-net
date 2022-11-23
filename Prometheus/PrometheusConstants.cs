using System.Net.Http.Headers;
using System.Text;

namespace Prometheus
{
    public static class PrometheusConstants
    {
        public const string
            TextFmtContentType = "text/plain",
            OpenMetricsContentType = "application/openmetrics-text";
        
        public const string
            ExporterContentType = TextFmtContentType + "; version=0.0.4; charset=utf-8",
            ExporterOpenMetricsContentType = OpenMetricsContentType + "; version=0.0.1; charset=utf-8";
        

        // ASP.NET requires a MediaTypeHeaderValue object
        public static readonly MediaTypeHeaderValue
            ExporterContentTypeValue = MediaTypeHeaderValue.Parse(ExporterContentType),
            ExporterOpenMetricsContentTypeValue = MediaTypeHeaderValue.Parse(ExporterOpenMetricsContentType);

        // Use UTF-8 encoding, but provide the flag to ensure the Unicode Byte Order Mark is never
        // pre-pended to the output stream.
        public static readonly Encoding ExportEncoding = new UTF8Encoding(false);
    }
}