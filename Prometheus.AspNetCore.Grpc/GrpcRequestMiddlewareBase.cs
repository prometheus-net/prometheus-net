using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Http;

namespace Prometheus;

// Modeled after HttpRequestMiddlewareBase, just with gRPC specific functionality.
internal abstract class GrpcRequestMiddlewareBase<TCollector, TChild>
    where TCollector : class, ICollector<TChild>
    where TChild : class, ICollectorChild
{
    /// <summary>
    /// The set of labels from among the defaults that this metric supports.
    /// 
    /// This set will be automatically extended with labels for additional
    /// route parameters when creating the default metric instance.
    /// </summary>
    protected abstract string[] DefaultLabels { get; }

    /// <summary>
    /// Creates the default metric instance with the specified set of labels.
    /// Only used if the caller does not provide a custom metric instance in the options.
    /// </summary>
    protected abstract TCollector CreateMetricInstance(string[] labelNames);

    /// <summary>
    /// The factory to use for creating the default metric for this middleware.
    /// Not used if a custom metric is alreaedy provided in options.
    /// </summary>
    protected MetricFactory MetricFactory { get; }

    private readonly TCollector _metric;

    protected GrpcRequestMiddlewareBase(GrpcMetricsOptionsBase? options, TCollector? customMetric)
    {
        MetricFactory = Metrics.WithCustomRegistry(options?.Registry ?? Metrics.DefaultRegistry);

        if (customMetric != null)
        {
            _metric = customMetric;
            ValidateNoUnexpectedLabelNames();
        }
        else
        {
            _metric = CreateMetricInstance(DefaultLabels);
        }
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
                    // Should never reach this point because we validate in ctor.
                    throw new NotSupportedException($"Unexpected label name on {_metric.Name}: {_metric.LabelNames[i]}");
            }
        }

        return _metric.WithLabels(labelValues);
    }

    /// <summary>
    /// If we use a custom metric, it should not have labels that are neither defaults nor additional route parameters.
    /// </summary>
    private void ValidateNoUnexpectedLabelNames()
    {
        var unexpected = _metric.LabelNames.Except(DefaultLabels);

        if (unexpected.Any())
            throw new ArgumentException($"Provided custom gRPC request metric instance for {GetType().Name} has some unexpected labels: {string.Join(", ", unexpected)}.");
    }
}