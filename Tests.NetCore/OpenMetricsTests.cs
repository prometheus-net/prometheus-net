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
    }
}
