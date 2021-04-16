using System.IO;
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
    }
}
