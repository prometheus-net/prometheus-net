using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpMetrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Prometheus.Tests.HttpExporter
{
    /// <summary>
    /// We assume that all the HTTP metrics implement the same route parameter to metric label mapping logic.
    /// Therefore, we only perform these tests on one of the metric types we measure, to avoid needless duplication.
    /// </summary>
    [TestClass]
    public sealed class RouteParameterMappingTests
    {
        private readonly DefaultHttpContext _context;
        private readonly RequestDelegate _next;

        private readonly CollectorRegistry _registry;
        private readonly MetricFactory _metrics;

        private const int TestStatusCode = 204;
        private const string TestMethod = "DELETE";
        private const string TestController = "controllerAbcde";
        private const string TestAction = "action1234";

        // We do not set up endpoint routing in tests, so this will always be empty string.
        private const string TestEndpoint = "";

        public RouteParameterMappingTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);

            _next = context => Task.CompletedTask;
            _context = new DefaultHttpContext();
        }

        private static readonly string[] DefaultLabelNamesPlusEndpoint = HttpRequestLabelNames.Default.Concat(new[] { HttpRequestLabelNames.Endpoint }).ToArray();
        private static readonly string[] DefaultLabelNamesPlusEndpointAndPage = HttpRequestLabelNames.Default.Concat(new[] { HttpRequestLabelNames.Endpoint, HttpRequestLabelNames.Page }).ToArray();

        [TestMethod]
        public void DefaultMetric_AppliesStandardLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpoint, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void DefaultMetric_WithCustomFactory_AppliesStandardLabelsAndFactoryLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController);

            var labelName = "static_label_1";
            var labelValue = "static_label_value_1";

            var factory = Metrics.WithCustomRegistry(_registry)
                .WithLabels(new Dictionary<string, string>
                {
                    { labelName, labelValue }
                });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                MetricFactory = factory
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpoint, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint
            }, child.InstanceLabels.Values.ToArray());

            var expectedFlattenedLabelNames = new[] { labelName }.Concat(DefaultLabelNamesPlusEndpoint).ToArray();
            var expectedFlattenedLabelValues = new[] { labelValue }.Concat(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint
            }).ToArray();

            CollectionAssert.AreEquivalent(expectedFlattenedLabelNames, child.FlattenedLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(expectedFlattenedLabelValues, child.FlattenedLabels.Values.ToArray());
        }

        [TestMethod]
        public void CustomMetric_WithNoLabels_AppliesNoLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "")
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            Assert.AreEqual(0, child.InstanceLabels.Length);
        }

        [TestMethod]
        public void CustomMetric_WithAllBuiltinLabels_AppliesLabels()
        {
            // Note that we do not configure Razor Pages, so there is no automatic "page" label.
            // However, we still expect it to be fine for custom metrics to include any builtin label at any time.
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "", HttpRequestLabelNames.All)
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(HttpRequestLabelNames.All, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint,
                "" // page
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void CustomMetric_WithStandardLabels_AppliesStandardLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "", HttpRequestLabelNames.Default)
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(HttpRequestLabelNames.Default, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void CustomMetric_WithExtendedLabels_AppliesExtendedLabels()
        {
            // Route parameters tracked:
            // foo = 123
            // bar = (missing)
            // method = excellent // remapped to route_method
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController,
                new[]
                {
                    ("foo", "123"),
                    ("method", "excellent")
                });

            var allLabelNames = HttpRequestLabelNames.Default.Concat(new[] { "foo", "bar", "route_method" }).ToArray();

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "", allLabelNames),
                AdditionalRouteParameters =
                {
                    "foo",
                    "bar",
                    new HttpRouteParameterMapping("method", "route_method")
                }
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(allLabelNames, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                "123", // foo
                "", // bar
                "excellent" // route_method
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_AppliesExtendedLabels()
        {
            // Route parameters tracked:
            // foo = 123
            // bar = (missing)
            // method = excellent // remapped to route_method
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController,
                new[]
                {
                    ("foo", "123"),
                    ("method", "excellent")
                });

            var allLabelNames = DefaultLabelNamesPlusEndpoint.Concat(new[] { "foo", "bar", "route_method" }).ToArray();

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                AdditionalRouteParameters =
                {
                    "foo",
                    "bar",
                    new HttpRouteParameterMapping("method", "route_method")
                }
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(allLabelNames, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint,
                "123", // foo
                "", // bar
                "excellent" // route_method
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void CustomMetric_WithUnexpectedLabels_Throws()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
                {
                    Counter = Metrics.CreateCounter("xxx", "", "unknown_label_name")
                });
            });
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_WithStandardParameterNameConflict_DoesNotThrow()
        {
            // It is fine to re-map one of the builtint parameters to something else. Sure, why not.
            new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                AdditionalRouteParameters =
                {
                    new HttpRouteParameterMapping("controller", "xxxxx")
                }
            });
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_WithStandardLabelNameConflict_Throws()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
                {
                    Registry = _registry,
                    AdditionalRouteParameters =
                    {
                        new HttpRouteParameterMapping("xxxxx", "controller")
                    }
                });
            });
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_WithDuplicateParameterName_DoesNotThrow()
        {
            // It is fine to have multiple mappings for the same parameter. Perhaps a bit useless in 99% of cases but perhaps useful in 1%.

            new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                AdditionalRouteParameters =
                {
                    new HttpRouteParameterMapping("foo", "bar"),
                    new HttpRouteParameterMapping("foo", "bang")
                }
            });
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_WithDuplicateLabelName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
                {
                    Registry = _registry,
                    AdditionalRouteParameters =
                    {
                        new HttpRouteParameterMapping("foo", "bar"),
                        new HttpRouteParameterMapping("quux", "bar")
                    }
                });
            });
        }

        [TestMethod]
        [DataRow(200, "2xx")]
        [DataRow(404, "4xx")]
        // Made up status code to verify we don't ever round status code values up
        [DataRow(599, "5xx")]
        public void DefaultMetric_WithReducedStatusCodeCardinality_ReducesStatusCodeCardinality(int statusCode, string expectedStatusLabel)
        {
            SetupHttpContext(_context, statusCode, TestMethod, TestAction, TestController);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                ReduceStatusCodeCardinality = true
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpoint, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                expectedStatusLabel,
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void DefaultMetric_WithPageFeatureEnabled_CreatesPageLabel()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, new[]
            {
                (HttpRequestLabelNames.Page, "page_name")
            });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                IncludePageLabelInDefaultsInternal = true
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpointAndPage, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint,
                "page_name"
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void DefaultMetric_WithPageFeatureEnabled_WithPageAsMappedRouteParameter_CreatesPageLabel()
        {
            // If it is both enabled by default logic and explicitly mapped as a route parameter, the two equivalent ways to add the label merge and we operate "normally".

            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, new[]
            {
                (HttpRequestLabelNames.Page, "page_name")
            });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                IncludePageLabelInDefaultsInternal = true,
                AdditionalRouteParameters = { HttpRequestLabelNames.Page }
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpointAndPage, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint,
                "page_name"
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void DefaultMetric_WithPageFeatureEnabled_WithCustomPageLabel_CustomLabelWins()
        {
            // If it is both enabled by default logic and explicitly mapped as a custom label, the custom label is used instead of the builtin logic.

            var canary = "Will the real page name stand up";

            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, new[]
            {
                (HttpRequestLabelNames.Page, "page_name")
            });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                IncludePageLabelInDefaultsInternal = true,
                CustomLabels =
                {
                    new HttpCustomLabel(HttpRequestLabelNames.Page, x => canary)
                }
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(DefaultLabelNamesPlusEndpointAndPage, child.InstanceLabels.Names.ToArray());
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestEndpoint,
                canary
            }, child.InstanceLabels.Values.ToArray());
        }

        [TestMethod]
        public void CustomMetric_WithPageFeatureEnabled_WithoutPageLabel_OperatesWithoutPageLabel()
        {
            // "page" feature is optional - if possible, it is actioned. But if custom metric, it is a no-op.

            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, new[]
            {
                (HttpRequestLabelNames.Page, "page_name")
            });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                IncludePageLabelInDefaultsInternal = true,
                Counter = _metrics.CreateCounter("xxx", "")
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            Assert.AreEqual(0, child.InstanceLabels.Names.Length);
        }

        [TestMethod]
        public void CustomMetric_WithPageFeatureEnabled_WithPageLabel_OperatesWithPageLabel()
        {
            // If "page" label is present, it needs to be filled no matter whether it came from custom or default metric.

            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, new[]
            {
                (HttpRequestLabelNames.Page, "page_name")
            });

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry,
                IncludePageLabelInDefaultsInternal = true,
                Counter = _metrics.CreateCounter("xxx", "", new[] { HttpRequestLabelNames.Page })
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            Assert.AreEqual(1, child.InstanceLabels.Names.Length);
            Assert.AreEqual(HttpRequestLabelNames.Page, child.InstanceLabels.Names.ToArray().Single());
            Assert.AreEqual("page_name", child.InstanceLabels.Values.ToArray().Single());
        }

        private static void SetupHttpContext(DefaultHttpContext context, int statusCode, string httpMethod, string action, string controller, (string name, string value)[] routeParameters = null)
        {
            context.Response.StatusCode = statusCode;
            context.Request.Method = httpMethod;

            var routing = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "Action", action }, { "Controller", controller } }
                }
            };

            if (routeParameters != null)
            {
                foreach (var parameter in routeParameters)
                    routing.RouteData.Values[parameter.name] = parameter.value;
            }

            context.Features[typeof(IRoutingFeature)] = routing;
        }

        internal class FakeRoutingFeature : IRoutingFeature
        {
            public RouteData RouteData { get; set; }
        }
    }
}
