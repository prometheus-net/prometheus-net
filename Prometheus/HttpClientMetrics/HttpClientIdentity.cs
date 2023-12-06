namespace Prometheus.HttpClientMetrics;

public sealed class HttpClientIdentity
{
    public static readonly HttpClientIdentity Default = new HttpClientIdentity("default");

    public string Name { get; }

    public HttpClientIdentity(string name)
    {
        Name = name;
    }
}
