using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpMetrics;

namespace Prometheus.Tests.HttpExporter
{
	[TestClass]
	public class AdditionalLabelsTests
	{
		private readonly DefaultHttpContext _context;
		private readonly RequestDelegate _next;

		private readonly CollectorRegistry _registry;

		private const int TestStatusCode = 204;
		private const string TestMethod = "DELETE";
		private const string TestController = "controllerAbcde";
		private const string TestAction = "action1234";

		private const string UserAgentLabel = "userAgent";
		private const string TestUserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";

		public AdditionalLabelsTests()
		{
			_registry = Metrics.NewCustomRegistry();

			_next = context => Task.CompletedTask;
			_context = new DefaultHttpContext();
		}

		[TestMethod]
		public void AdditionalLabels_AppliesUserAgent()
		{
			HttpExporterTestDataProvider.SetupHttpContext(_context, TestStatusCode, TestMethod, TestAction, TestController, TestUserAgent);

			var middleware = new HttpRequestCountMiddleware(_next, new HttpRequestCountOptions
			{
				Registry = _registry,
				AdditionalLabels = new Dictionary<string, Func<HttpContext, string>>
				{
					{UserAgentLabel, HttpExporterTestDataProvider.GetUserAgent}
				}
			});

			var child = (ChildBase)middleware.CreateChild(_context);

			var expectedLabels = HttpRequestLabelNames.All.Concat(new[] { UserAgentLabel }).ToArray();

			CollectionAssert.AreEquivalent(expectedLabels, child.Labels.Names);

			CollectionAssert.AreEquivalent(new[]
			{
				TestStatusCode.ToString(),
				TestMethod,
				TestAction,
				TestController,
				TestUserAgent
			}, child.Labels.Values);
		}
	}
}