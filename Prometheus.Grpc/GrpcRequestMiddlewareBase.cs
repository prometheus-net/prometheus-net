using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Grpc.AspNetCore.Server;

namespace Prometheus.Grpc
{
    public abstract class GrpcRequestMiddlewareBase<TCollector, TChild>
        where TCollector : ICollector<TChild>
        where TChild : class, ICollectorChild
    {
        protected abstract string[] AllowedLabelNames { get; }

        protected readonly TCollector _metric;

        protected GrpcRequestMiddlewareBase(TCollector metric)
        {
            if (metric == null) throw new ArgumentException(nameof(metric));

            if (!LabelsAreValid(metric.LabelNames))
            {
                throw new ArgumentException(
                    $"{metric.Name} may only use labels from the following set: {string.Join(", ", AllowedLabelNames)}");
            }

            _metric = metric;
        }

        protected TChild? CreateChild(HttpContext context)
        {
            var metadata = context.GetEndpoint()?.Metadata?.GetMetadata<GrpcMethodMetadata>();
            if (metadata == null)
            {
                // Not a gRPC request
                return null;
            }

            if (!_metric.LabelNames.Any())
            {
                return _metric.Unlabelled;
            }

            return CreateChild(context, metadata);
        }

        protected TChild CreateChild(HttpContext context, GrpcMethodMetadata metadata)
        {
            var labelValues = new string[_metric.LabelNames.Length];

            for (var i = 0; i < labelValues.Length; i++)
            {
                switch (_metric.LabelNames[i])
                {
                    case GrpcRequestLabelNames.Service:
                        labelValues[i] = metadata.Method.ServiceName;
                        break;
                    case GrpcRequestLabelNames.Method:
                        labelValues[i] = metadata.Method.Name;
                        break;
                    default:
                        throw new NotSupportedException($"Unexpected label name on {_metric.Name}: {_metric.LabelNames[i]}");
                }
            }

            return _metric.WithLabels(labelValues);
        }

        private bool LabelsAreValid(string[] labelNames)
        {
            return !labelNames.Except(AllowedLabelNames).Any();
        }
    }
}