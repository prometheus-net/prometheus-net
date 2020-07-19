using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpMetrics;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing.Patterns;

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
        private const string TestRoutePattern = "controllerAbcde/action1234";

        public RouteParameterMappingTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);

            _next = context => Task.CompletedTask;
            _context = new DefaultHttpContext();
        }

        [TestMethod]
        public void DefaultMetric_AppliesStandardLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestRoutePattern);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(HttpRequestLabelNames.All, child.Labels.Names);
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestRoutePattern
            }, child.Labels.Values);
        }

        [TestMethod]
        public void CustomMetric_WithNoLabels_AppliesNoLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestRoutePattern);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "")
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            Assert.AreEqual(0, child.Labels.Count);
        }

        [TestMethod]
        public void CustomMetric_WithStandardLabels_AppliesStandardLabels()
        {
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestRoutePattern);

            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Counter = _metrics.CreateCounter("xxx", "", HttpRequestLabelNames.All)
            });
            var child = (ChildBase)middleware.CreateChild(_context);

            CollectionAssert.AreEquivalent(HttpRequestLabelNames.All, child.Labels.Names);
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestRoutePattern
            }, child.Labels.Values);
        }

        [TestMethod]
        public void CustomMetric_WithExtendedLabels_AppliesExtendedLabels()
        {
            // Route parameters tracked:
            // foo = 123
            // bar = (missing)
            // method = excellent // remapped to route_method
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestRoutePattern,
                new[]
                {
                    ("foo", "123"),
                    ("method", "excellent")
                });

            var allLabelNames = HttpRequestLabelNames.All.Concat(new[] { "foo", "bar", "route_method" }).ToArray();

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

            CollectionAssert.AreEquivalent(allLabelNames, child.Labels.Names);
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestRoutePattern,
                "123", // foo
                "", // bar
                "excellent" // route_method
            }, child.Labels.Values);
        }

        [TestMethod]
        public void DefaultMetric_WithExtendedLabels_AppliesExtendedLabels()
        {
            // Route parameters tracked:
            // foo = 123
            // bar = (missing)
            // method = excellent // remapped to route_method
            SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestRoutePattern,
                new[]
                {
                    ("foo", "123"),
                    ("method", "excellent")
                });

            var allLabelNames = HttpRequestLabelNames.All.Concat(new[] { "foo", "bar", "route_method" }).ToArray();

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

            CollectionAssert.AreEquivalent(allLabelNames, child.Labels.Names);
            CollectionAssert.AreEquivalent(new[]
            {
                TestStatusCode.ToString(),
                TestMethod,
                TestAction,
                TestController,
                TestRoutePattern,
                "123", // foo
                "", // bar
                "excellent" // route_method
            }, child.Labels.Values);
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
        public void DefaultMetric_WithExtendedLabels_WithStandardParameterNameConflict_Throws()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
                {
                    Registry = _registry,
                    AdditionalRouteParameters =
                    {
                        new HttpRouteParameterMapping("controller", "xxxxx")
                    }
                });
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
        public void DefaultMetric_WithExtendedLabels_WithDuplicateParameterName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(delegate
            {
                new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
                {
                    Registry = _registry,
                    AdditionalRouteParameters =
                    {
                        new HttpRouteParameterMapping("foo", "bar"),
                        new HttpRouteParameterMapping("foo", "bang")
                    }
                });
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
        public void DefaultMetric_WhenNoRouteEndpoint_PopulatesRoutePatternLabelCorrectly()
        {
            _context.Features[typeof(IEndpointFeature)] = new FakeEndpointFeature
            {
                Endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), string.Empty)
            };
            
            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry
            });
            
            var child = (ChildBase)middleware.CreateChild(_context);

            var labelValue = GetLabelValueOrDefault(child.Labels, HttpRequestLabelNames.RoutePattern);

            Assert.AreEqual(string.Empty, labelValue);
        }

        [TestMethod]
        public void DefaultMetric_WhenNoRoutepatternRawtext_PopulatesRoutepatternLabelCorrectly()
        {
            var pattern = RoutePatternFactory.Pattern((string)null);
            _context.Features[typeof(IEndpointFeature)] = new FakeEndpointFeature
            {
                Endpoint = new RouteEndpoint(
                    _ => Task.CompletedTask,
                    pattern,
                    0,
                    new EndpointMetadataCollection(),
                    string.Empty
                )
            };
            var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
            {
                Registry = _registry
            });
            
            var child = (ChildBase)middleware.CreateChild(_context);
            
            var labelValue = GetLabelValueOrDefault(child.Labels, HttpRequestLabelNames.RoutePattern);

            Assert.AreEqual(string.Empty, labelValue);
        }

        private static void SetupHttpContext(DefaultHttpContext context, int statusCode, string httpMethod, string action, string controller, string routePattern, (string name, string value)[] routeParameters = null)
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

            var pattern = RoutePatternFactory.Pattern(routePattern);

            if (routeParameters != null)
            {
                foreach (var parameter in routeParameters)
                    routing.RouteData.Values[parameter.name] = parameter.value;
            }

            context.Features[typeof(IRoutingFeature)] = routing;
            context.Features[typeof(IEndpointFeature)] = new FakeEndpointFeature
            {
                Endpoint = new RouteEndpoint(
                    _ => Task.CompletedTask,
                    pattern,
                    0,
                    new EndpointMetadataCollection(),
                    string.Empty
                )
            };
        }

        private static string GetLabelValueOrDefault(Labels labels, string name)
        {
            return labels.Names
                .Zip(labels.Values, (n, v) => (n, v))
                .FirstOrDefault(pair => pair.n == name).v;
        }

        internal class FakeRoutingFeature : IRoutingFeature
        {
            public RouteData RouteData { get; set; }
        }

        internal class FakeEndpointFeature : IEndpointFeature
        {
            public Endpoint Endpoint { get; set; }
        }
    }
}
