using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace Prometheus.Tests
{
    public sealed class AsciiFormatterTests
    {
        [DataTestMethod]
        [DataRow("simple-label-value-1")]
        [DataRow("with\nlinebreaks")]
        [DataRow("with\nlinebreaks and \\slashes and quotes \"")]
        public void family_should_be_formatted_to_one_line(string labelValue)
        {
            using (var ms = new MemoryStream())
            {
                var metricFamily = new MetricFamilyData
                {
                    Name = "family1",
                    Help = "help",
                    Type = MetricType.Counter,
                };

                var metricCounter = new CounterData { Value = 100 };
                metricFamily.Metrics.Add(new MetricData
                {
                    Counter = metricCounter,
                    Labels = new[]
                    {
                        new LabelPairData {Name = "label1", Value = labelValue }
                    }
                });

                AsciiFormatter.Format(ms, new[]
                {
                    metricFamily
                });

                using (var sr = new StringReader(Encoding.UTF8.GetString(ms.ToArray())))
                {
                    var linesCount = 0;
                    var line = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                        Console.WriteLine(line);
                        linesCount += 1;
                    }
                    Assert.AreEqual(3, linesCount);
                }
            }
        }
    }
}
