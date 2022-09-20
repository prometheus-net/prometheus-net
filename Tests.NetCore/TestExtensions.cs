using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    internal static class TestExtensions
    {
        public static async Task<string> CollectAndSerializeToStringAsync(this CollectorRegistry registry)
        {
            var buffer = new MemoryStream();
            await registry.CollectAndExportAsTextAsync(buffer);
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
