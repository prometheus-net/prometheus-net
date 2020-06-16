using Prometheus;
using System;

namespace tester
{
    sealed class GrpcMiddlewareTester : Tester
    {
        public override IMetricServer InitializeMetricServer()
        {
            throw new NotImplementedException("gRPC is only available under .NET Core");
        }
    }
}
