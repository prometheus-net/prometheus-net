namespace Prometheus.HttpClientMetrics;

/// <summary>
/// Label names reserved for the use by the HttpClient metrics.
/// </summary>
public static class HttpClientRequestLabelNames
{
    public const string Method = "method";
    public const string Host = "host";
    public const string Client = "client";
    public const string Code = "code";
    public const string Path = "path";

    public static readonly string[] All =
    {
        Method,
        Host,
        Client,
        Code,
        Path
    };

    // The labels known before receiving the response.
    // Everything except the response status code, basically.
    public static readonly string[] KnownInAdvance =
    {
        Method,
        Host,
        Client,
        Path
    };
}
