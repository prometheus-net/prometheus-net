using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests
{
    [TestClass]
    public class OpenMetricsTests
    {
        [TestMethod]
        public void Serializer_ExposesContentType()
        {
            using(var stream = new MemoryStream())
            {
                var serializer = new TextSerializer(stream);
                Assert.AreEqual(serializer.ContentType(), PrometheusConstants.ExporterContentType);

                serializer = new TextSerializer(stream, PrometheusConstants.ExporterContentTypeOpenMetrics);
                Assert.AreEqual(serializer.ContentType(), PrometheusConstants.ExporterContentTypeOpenMetrics);
            }
        }

        [TestMethod]
        public async Task Serializer_WritesEofBytes()
        {
            MemoryStream stream;
            TextSerializer serializer;
            string text;

            // The legacy format can be totally empty.
            using(stream = new MemoryStream())
            {
                serializer = new TextSerializer(stream);
                await serializer.FlushAsync(default);
                stream.Position = 0;
                text = new StreamReader(stream).ReadToEnd();
                Assert.AreEqual(text, "");
            }

            // The OpenMetrics format must end with "# EOF".
            using(stream = new MemoryStream())
            {
                serializer = new TextSerializer(stream, PrometheusConstants.ExporterContentTypeOpenMetrics);
                await serializer.FlushAsync(default);
                stream.Position = 0;
                text = new StreamReader(stream).ReadToEnd();
                StringAssert.EndsWith(text, "# EOF");
            }
        }

        [TestMethod]
        public async Task Serializer_StripsCounterSuffix()
        {
            var registry = Metrics.NewCustomRegistry();
            var metrics = Metrics.WithCustomRegistry(registry);
            var counter = metrics.CreateCounter("requests_processed_total", "help");

            using(var stream = new MemoryStream())
            {
                var serializer = new TextSerializer(stream, PrometheusConstants.ExporterContentTypeOpenMetrics);
                await registry.CollectAndSerializeAsync(serializer, default);
                stream.Position = 0;
                string text = new StreamReader(stream).ReadToEnd();
                StringAssert.StartsWith(text, "# HELP requests_processed help\n# TYPE requests_processed counter\nrequests_processed_total 0");
            }
        }

        [TestMethod]
        public async Task Serializer_MarksNonTotalCountersAsUnknown()
        {
            var registry = Metrics.NewCustomRegistry();
            var metrics = Metrics.WithCustomRegistry(registry);
            var counter = metrics.CreateCounter("requests_processed", "help"); // No "_total".

            using(var stream = new MemoryStream())
            {
                var serializer = new TextSerializer(stream, PrometheusConstants.ExporterContentTypeOpenMetrics);
                await registry.CollectAndSerializeAsync(serializer, default);
                stream.Position = 0;
                string text = new StreamReader(stream).ReadToEnd();
                StringAssert.StartsWith(text, "# HELP requests_processed help\n# TYPE requests_processed unknown\nrequests_processed 0");
            }
        }
    }
}
