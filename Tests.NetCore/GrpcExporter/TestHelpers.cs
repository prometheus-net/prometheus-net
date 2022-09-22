using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;
using System.Threading.Tasks;

namespace Prometheus.Tests.GrpcExporter
{
    public static class TestHelpers
    {
        public static void SetupHttpContext(DefaultHttpContext context, string expectedService,
            string expectedMethod)
        {
            var method = Substitute.For<IMethod>();
            method.ServiceName.Returns(expectedService);
            method.Name.Returns(expectedMethod);

            var metadata = new GrpcMethodMetadata(typeof(TestHelpers), method);

            var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "gRPC");

            var endpointFeature = Substitute.For<IEndpointFeature>();
            endpointFeature.Endpoint.Returns(endpoint);

            context.Features[typeof(IEndpointFeature)] = endpointFeature;
        }
    }
}
