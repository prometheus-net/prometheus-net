using Prometheus;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

// This sample demonstrates how to integrate prometheus-net into a .NET Framework web app.
// 
// NuGet packages required:
// * prometheus-net.NetFramework.AspNet

namespace Sample.Web.NetFramework
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Enable the /metrics page to export Prometheus metrics.
            // Open http://localhost:xxxx/metrics to see the metrics.
            //
            // Metrics published in this sample:
            // * built-in process metrics giving basic information about the .NET runtime (enabled by default)
            AspNetMetricServer.RegisterRoutes(GlobalConfiguration.Configuration);

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
