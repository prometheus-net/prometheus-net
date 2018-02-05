using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;
using System;
using System.Collections.Generic;

namespace tester
{
    /// <summary>
    /// This is an example of how to implement a collector that exposes data retrieved from an external source.
    /// For example, Windows performance counters or the monitoring API of another application.
    /// </summary>
    sealed class ExternalDataCollector : ICollector
    {
        public ExternalDataCollector()
        {
        }

        // Only used as a key in collector registry.
        public string Name { get; } = "example_external_data_collector";

        // Only used during registration - we can return data with any labels we want.
        public string[] LabelNames { get; } = new string[0];

        // Example metrics we pretend to collect.
        private sealed class ServiceMetrics
        {
            public string Name { get; set; }

            public double HandledRequestCount { get; set; }
            public double SuccessRatio { get; set; }

            public double Pi { get; set; }

            public List<LabelPair> GetLabels()
            {
                return new List<LabelPair>
                {
                    new LabelPair
                    {
                        name = "service",
                        value = Name
                    },
                    new LabelPair
                    {
                        name = "domain",
                        value = "example.test"
                    }
                };
            }
        }

        public IEnumerable<MetricFamily> Collect()
        {
            // NB! Prometheus is not at all tolerant of slow metrics export - the collection should occur in milliseconds,
            // not seconds. If this data takes a long time to gather, collect it as a parallel activity, only reporting
            // last known state in the actual metrics collector.

            // We pretend that we somehow query external services for their metrics.
            var dataForServices = new[]
            {
                new ServiceMetrics
                {
                    Name = "calculator.worldcom.test",

                    HandledRequestCount = 2184181,
                    SuccessRatio = 0.9924952,

                    Pi = Math.PI
                },

                new ServiceMetrics
                {
                    Name = "accounting.enron.test",

                    HandledRequestCount = 8428278281161200,
                    SuccessRatio = 0.999591951005,

                    Pi = Math.PI + 0.51
                },

                new ServiceMetrics
                {
                    Name = "more.test",

                    HandledRequestCount = 15,
                    SuccessRatio = 2.0 / 15,

                    Pi = 3
                }
            };

            // Now we need to transform the collected data into Prometheus data structures.
            // First, formulate the metric families that represent the types of data we collect.
            var piFamily = new MetricFamily
            {
                name = "example_value_of_pi",
                type = MetricType.GAUGE,
                help = "Indicates the current value of pi in the service that was queried",
            };

            var requestCountFamily = new MetricFamily
            {
                name = "example_handled_request_count",
                type = MetricType.COUNTER,
                help = "Number of handled requests, counting both successful and unsuccessful requests"
            };

            var successRatioFamily = new MetricFamily
            {
                name = "example_success_ratio",
                type = MetricType.GAUGE,
                help = "Ratio of succeeded requests to all requests."
            };

            // Then fill the families with the actual data rows.
            foreach (var service in dataForServices)
            {
                piFamily.metric.Add(new Metric
                {
                    gauge = new Gauge
                    {
                        value = service.Pi
                    },
                    label = service.GetLabels()
                });

                requestCountFamily.metric.Add(new Metric
                {
                    counter = new Counter
                    {
                        value = service.HandledRequestCount
                    },
                    label = service.GetLabels()
                });

                successRatioFamily.metric.Add(new Metric
                {
                    gauge = new Gauge
                    {
                        value = service.SuccessRatio
                    },
                    label = service.GetLabels()
                });
            }

            // That's it. Just got to return the metric families for export now.
            yield return piFamily;
            yield return requestCountFamily;
            yield return successRatioFamily;
        }
    }
}
