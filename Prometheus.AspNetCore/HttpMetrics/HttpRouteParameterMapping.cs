namespace Prometheus.HttpMetrics;

/// <summary>
/// Maps an HTTP route parameter name to a Prometheus label name.
/// </summary>
/// <remarks>
/// Typically, the parameter name and the label name will be equal.
/// The purpose of this is to enable capture of route parameters that conflict with built-in label names like "method" (HTTP method).
/// </remarks>
public sealed class HttpRouteParameterMapping
{
    /// <summary>
    /// Name of the HTTP route parameter.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Name of the Prometheus label.
    /// </summary>
    public string LabelName { get; }

    public HttpRouteParameterMapping(string name)
    {
        Collector.ValidateLabelName(name);

        ParameterName = name;
        LabelName = name;
    }

    public HttpRouteParameterMapping(string parameterName, string labelName)
    {
        Collector.ValidateLabelName(labelName);

        ParameterName = parameterName;
        LabelName = labelName;
    }

    public static implicit operator HttpRouteParameterMapping(string name) => new HttpRouteParameterMapping(name);
}
