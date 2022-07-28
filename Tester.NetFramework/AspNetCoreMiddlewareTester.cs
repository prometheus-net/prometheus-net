using Prometheus;
using System;

namespace tester
{
    sealed class AspNetCoreMiddlewareTester : Tester
    {
        public override IMetricServer InitializeMetricServer()
        {
            throw new NotImplementedException("ASP.NET Core tester is only available under .NET Core");
        }
    }
}
