using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Internal
{
    internal static class AsciiFormatter
    {
        // Use UTF-8 encoding, but provide the flag to ensure the Unicode Byte Order Mark is never
        // pre-pended to the output stream.
        private static readonly Encoding Encoding = new UTF8Encoding(false);

        public static void Format(Stream destination, IEnumerable<MetricFamily> metrics)
        {
            var metricFamilys = metrics.ToArray();

            using (var streamWriter = new StreamWriter(destination, Encoding))
            {
                streamWriter.NewLine = "\n";
                foreach (var metricFamily in metricFamilys)
                {
                    WriteFamily(streamWriter, metricFamily);
                }
            }
        }

        private static void WriteFamily(StreamWriter streamWriter, MetricFamily metricFamily)
        {
            streamWriter.WriteLine("# HELP {0} {1}", metricFamily.name, metricFamily.help);
            streamWriter.WriteLine("# TYPE {0} {1}", metricFamily.name, metricFamily.type);
            foreach (var metric in metricFamily.metric)
            {
                WriteMetric(streamWriter, metricFamily, metric);
            }
        }

        private static void WriteMetric(StreamWriter streamWriter, MetricFamily family, Metric metric)
        {
            var familyName = family.name;

            if (metric.gauge!=null)
            {
                streamWriter.WriteLine(SimpleValue(familyName, metric.gauge.value, metric.label));
            }
            else if (metric.counter!=null)
            {
                streamWriter.WriteLine(SimpleValue(familyName, metric.counter.value, metric.label));
            }
            else if (metric.summary != null)
            {
                streamWriter.WriteLine(SimpleValue(familyName, metric.summary.sample_sum, metric.label, "_sum"));
                streamWriter.WriteLine(SimpleValue(familyName, metric.summary.sample_count, metric.label, "_count"));

                foreach (var quantileValuePair in metric.summary.quantile)
                {
                    var quantile = double.IsPositiveInfinity(quantileValuePair.quantile) ? "+Inf" : quantileValuePair.quantile.ToString(CultureInfo.InvariantCulture);
                    streamWriter.WriteLine(SimpleValue(familyName, quantileValuePair.value, metric.label.Concat(new []{new LabelPair{name= "quantile", value = quantile}})));
                }
            }
            else if (metric.histogram != null)
            {
                streamWriter.WriteLine(SimpleValue(familyName, metric.histogram.sample_sum, metric.label, "_sum"));
                streamWriter.WriteLine(SimpleValue(familyName, metric.histogram.sample_count, metric.label, "_count"));
                foreach (var bucket in metric.histogram.bucket)
                {
                    var value = double.IsPositiveInfinity(bucket.upper_bound) ? "+Inf" : bucket.upper_bound.ToString(CultureInfo.InvariantCulture);
                    streamWriter.WriteLine(SimpleValue(familyName, bucket.cumulative_count, metric.label.Concat(new []{new LabelPair{name = "le", value = value}}), "_bucket"));
                }
            }
            else
            {
                //not supported
            }
        }

        private static string WithLabels(string familyName, IEnumerable<LabelPair> labels)
        {
            var labelPairs = labels as LabelPair[] ?? labels.ToArray();
            if (labelPairs.Length == 0)
            {
                return familyName;
            }
            return string.Format(
                "{0}{{{1}}}", familyName,
                string.Join(",", labelPairs.Select(l => string.Format("{0}=\"{1}\"", l.name, EscapeValue(l.value)))));
        }

        private static string EscapeValue(string val)
        {
            return val
                    .Replace("\\", @"\\")
                    .Replace("\n", @"\n")
                    .Replace("\"", @"\""");
        }

        private static string SimpleValue(string family, double value, IEnumerable<LabelPair> labels, string namePostfix = null)
        {
            return string.Format("{0} {1}", WithLabels(family+(namePostfix ?? ""), labels), value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
