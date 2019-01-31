using Prometheus.DataContracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Prometheus
{
    internal static class AsciiFormatter
    {
        // Use UTF-8 encoding, but provide the flag to ensure the Unicode Byte Order Mark is never
        // pre-pended to the output stream.
        private static readonly Encoding Encoding = new UTF8Encoding(false);

        public static void Format(Stream destination, IEnumerable<MetricFamily> metrics)
        {
            // Leave stream open as we are just using it, not the owner of the stream!
            using (var writer = new StreamWriter(destination, Encoding, bufferSize: 1024, leaveOpen: true))
            {
                writer.NewLine = "\n";

                foreach (var family in metrics)
                {
                    WriteFamily(writer, family);
                }
            }
        }

        private static void WriteFamily(StreamWriter writer, MetricFamily family)
        {
            // # HELP familyname helptext
            writer.Write("# HELP ");
            writer.Write(family.name);
            writer.Write(" ");
            writer.WriteLine(family.help);

            // # TYPE familyname type
            writer.Write("# TYPE ");
            writer.Write(family.name);
            writer.Write(" ");
            writer.WriteLine(family.type.ToString().ToLowerInvariant());

            foreach (var metric in family.metric)
            {
                WriteMetric(writer, family, metric);
            }
        }

        private static void WriteMetric(StreamWriter writer, MetricFamily family, Metric metric)
        {
            var familyName = family.name;

            if (metric.gauge != null)
            {
                WriteMetricWithLabels(writer, familyName, null, metric.gauge.value, metric.label);
            }
            else if (metric.counter != null)
            {
                WriteMetricWithLabels(writer, familyName, null, metric.counter.value, metric.label);
            }
            else if (metric.summary != null)
            {
                WriteMetricWithLabels(writer, familyName, "_sum", metric.summary.sample_sum, metric.label);
                WriteMetricWithLabels(writer, familyName, "_count", metric.summary.sample_count, metric.label);

                foreach (var quantileValuePair in metric.summary.quantile)
                {
                    var quantile = double.IsPositiveInfinity(quantileValuePair.quantile) ? "+Inf" : quantileValuePair.quantile.ToString(CultureInfo.InvariantCulture);

                    var quantileLabels = metric.label.Concat(new[] { new LabelPair { name = "quantile", value = quantile } });

                    WriteMetricWithLabels(writer, familyName, null, quantileValuePair.value, quantileLabels);
                }
            }
            else if (metric.histogram != null)
            {
                WriteMetricWithLabels(writer, familyName, "_sum", metric.histogram.sample_sum, metric.label);
                WriteMetricWithLabels(writer, familyName, "_count", metric.histogram.sample_count, metric.label);

                foreach (var bucket in metric.histogram.bucket)
                {
                    var value = double.IsPositiveInfinity(bucket.upper_bound) ? "+Inf" : bucket.upper_bound.ToString(CultureInfo.InvariantCulture);

                    var bucketLabels = metric.label.Concat(new[] { new LabelPair { name = "le", value = value } });

                    WriteMetricWithLabels(writer, familyName, "_bucket", bucket.cumulative_count, bucketLabels);
                }
            }
            else
            {
                throw new NotSupportedException($"Metric {familyName} cannot be exported because it does not carry data of any known type.");
            }
        }

        private static void WriteMetricWithLabels(StreamWriter writer, string familyName, string postfix, double value, IEnumerable<LabelPair> labels)
        {
            // familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"} value
            writer.Write(familyName);

            if (postfix != null)
                writer.Write(postfix);

            if (labels?.Any() == true)
            {
                writer.Write('{');

                bool firstLabel = true;
                foreach (var label in labels)
                {
                    if (!firstLabel)
                        writer.Write(',');

                    firstLabel = false;

                    writer.Write(label.name);
                    writer.Write("=\"");

                    // Have to escape the label values!
                    writer.Write(EscapeLabelValue(label.value));

                    writer.Write('"');
                }

                writer.Write('}');
            }

            writer.Write(' ');
            writer.WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }

        private static string EscapeLabelValue(string value)
        {
            return value
                    .Replace("\\", @"\\")
                    .Replace("\n", @"\n")
                    .Replace("\"", @"\""");
        }
    }
}
