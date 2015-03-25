using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Prometheus.Internal
{
    public class ProtoFormatter
    {
        public static void Format(Stream destination, IEnumerable<io.prometheus.client.MetricFamily> metrics)
        {
            var metricFamilys = metrics.ToArray();
            foreach (var metricFamily in metricFamilys)
            {
                Serializer.SerializeWithLengthPrefix(destination, metricFamily, PrefixStyle.Base128, 0);
            }
        }
    }
}