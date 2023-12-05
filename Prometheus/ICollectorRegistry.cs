namespace Prometheus;

/// <summary>
/// Allows for substitution of CollectorRegistry in tests.
/// Not used by prometheus-net itself - you cannot provide your own implementation to prometheus-net code, only to your own code.
/// </summary>
public interface ICollectorRegistry
{
    void AddBeforeCollectCallback(Action callback);
    void AddBeforeCollectCallback(Func<CancellationToken, Task> callback);

    IEnumerable<KeyValuePair<string, string>> StaticLabels { get; }
    void SetStaticLabels(IDictionary<string, string> labels);

    Task CollectAndExportAsTextAsync(Stream to, ExpositionFormat format = ExpositionFormat.PrometheusText, CancellationToken cancel = default);
}
