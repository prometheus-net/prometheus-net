namespace Prometheus;

public sealed class MetricPusherOptions
{
    internal static readonly MetricPusherOptions Default = new();

    public string? Endpoint { get; set; }
    public string? Job { get; set; }
    public string? Instance { get; set; }
    public long IntervalMilliseconds { get; set; } = 1000;
    public IEnumerable<Tuple<string, string>>? AdditionalLabels { get; set; }
    public CollectorRegistry? Registry { get; set; }

    /// <summary>
    /// Callback for when a metric push fails.
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// If null, a singleton HttpClient will be used.
    /// </summary>
    public Func<HttpClient>? HttpClientProvider { get; set; }

    /// <summary>
    /// If true, replace the metrics in the group (identified by Job, Instance, AdditionalLabels).
    ///
    /// Replace means a HTTP PUT request will be made, otherwise a HTTP POST request will be made (which means add metrics to the group, if it already exists).
    ///
    /// Note: Other implementations of the pushgateway client default to replace, however to preserve backwards compatibility this implementation defaults to add.
    /// </summary>
    public bool ReplaceOnPush { get; set; } = false;
}
