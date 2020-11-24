using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Prometheus.Tests.HttpExporter
{
	public static class HttpExporterTestDataProvider
	{
		internal static void SetupHttpContext(
			DefaultHttpContext context,
			int statusCode,
			string httpMethod,
			string action,
			string controller,
			string userAgent = null,
			(string name, string value)[] routeParameters = null)
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

			if (userAgent != null)
			{
				context.Request.Headers.Add("User-Agent", userAgent);
			}

			if (routeParameters != null)
			{
				foreach (var parameter in routeParameters)
					routing.RouteData.Values[parameter.name] = parameter.value;
			}

			context.Features[typeof(IRoutingFeature)] = routing;
		}

		internal static string GetUserAgent(HttpContext context) =>
			context.Request.Headers.TryGetValue("User-Agent", out var userAgentValue) ? (string)userAgentValue : string.Empty;

		internal class FakeRoutingFeature : IRoutingFeature
		{
			public RouteData RouteData { get; set; }
		}
	}
}