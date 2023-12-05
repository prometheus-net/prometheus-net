using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace Prometheus.HttpMetrics;

/// <summary>
/// This base class performs the data management necessary to associate the correct labels and values
/// with HTTP request metrics, depending on the options the user has provided for the HTTP metric middleware.
/// 
/// The following labels are supported:
/// 'code' (HTTP status code)
/// 'method' (HTTP request method)
/// 'controller' (The Controller used to fulfill the HTTP request)
/// 'action' (The Action used to fulfill the HTTP request)
/// Any other label - from one of:
/// * HTTP route parameter (if name/mapping specified in options; name need not match).
/// * custom logic (callback decides value for each request)
/// 
/// The 'code' and 'method' data are taken from the current HTTP context.
/// 'controller', 'action' and route parameter labels will be taken from the request routing information.
/// 
/// If a custom metric is provided in the options, it must not be missing any labels for explicitly defined
/// custom route parameters. However, it is permitted to lack any of the default labels (code/method/...).
/// </summary>
internal abstract class HttpRequestMiddlewareBase<TCollector, TChild>
    where TCollector : class, ICollector<TChild>
    where TChild : class, ICollectorChild
{
    /// <summary>
    /// The set of labels from among the defaults that this metric supports.
    /// 
    /// This set will be automatically extended with labels for additional
    /// route parameters and custom labels when creating the default metric instance.
    /// 
    /// It will also be extended by additional built-in logic (page, endpoint).
    /// </summary>
    protected abstract string[] BaselineLabels { get; }

    /// <summary>
    /// Creates the default metric instance with the specified set of labels.
    /// Only used if the caller does not provide a custom metric instance in the options.
    /// </summary>
    protected abstract TCollector CreateMetricInstance(string[] labelNames);

    /// <summary>
    /// The factory to use for creating the default metric for this middleware.
    /// Not used if a custom metric is already provided in options.
    /// </summary>
    protected IMetricFactory MetricFactory { get; }

    private readonly List<HttpRouteParameterMapping> _additionalRouteParameters;
    private readonly List<HttpCustomLabel> _customLabels;
    private readonly TCollector _metric;

    // For labels that are route parameter mappings.
    private readonly Dictionary<string, string> _labelToRouteParameterMap;

    // For labels that use a custom value provider.
    private readonly Dictionary<string, Func<HttpContext, string>> _labelToValueProviderMap;

    private readonly bool _labelsRequireRouteData;
    private readonly bool _reduceStatusCodeCardinality;

    protected HttpRequestMiddlewareBase(HttpMetricsOptionsBase options, TCollector? customMetric)
    {
        MetricFactory = options.MetricFactory ?? Metrics.WithCustomRegistry(options.Registry ?? Metrics.DefaultRegistry);

        _additionalRouteParameters = options.AdditionalRouteParameters ?? new List<HttpRouteParameterMapping>(0);
        _customLabels = options.CustomLabels ?? new List<HttpCustomLabel>(0);

        if (options.IncludePageLabelInDefaultsInternal)
            AddPageLabelIfNoConflict(customMetric);

        AddEndpointLabelIfNoConflict(customMetric);

        ValidateMappings();
        _labelToRouteParameterMap = CreateLabelToRouteParameterMap();
        _reduceStatusCodeCardinality = options?.ReduceStatusCodeCardinality ?? false;
        _labelToValueProviderMap = CreateLabelToValueProviderMap();

        if (customMetric != null)
        {
            _metric = customMetric;

            ValidateNoUnexpectedLabelNames();
            ValidateAdditionalRouteParametersPresentInMetricLabelNames();
            ValidateCustomLabelsPresentInMetricLabelNames();
        }
        else
        {
            _metric = CreateMetricInstance(CreateDefaultLabelSet());
        }

        _labelsRequireRouteData = _metric.LabelNames.Except(HttpRequestLabelNames.NonRouteSpecific).Any();
    }

    private void AddPageLabelIfNoConflict(TCollector? customMetric)
    {
        // We were asked to add the "page" label because Razor Pages was detected.
        // We will only do this if nothing else has already occupied the "page" label.
        // If a custom metric is used, we also skip this if it has no "page" label name defined.
        //
        // The possible conflicts are:
        // * an existing route parameter mapping (which works out the same as our logic, so fine)
        // * custom logic that defines a "page" label (in which case we allow it to win, for backward compatibility).

        if (_additionalRouteParameters.Any(x => x.LabelName == HttpRequestLabelNames.Page))
            return;

        if (_customLabels.Any(x => x.LabelName == HttpRequestLabelNames.Page))
            return;

        if (customMetric != null && !customMetric.LabelNames.Contains(HttpRequestLabelNames.Page))
            return;

        // If we got so far, we are good - all preconditions for adding "page" label exist.
        _additionalRouteParameters.Add(new HttpRouteParameterMapping("page"));
    }

    private void AddEndpointLabelIfNoConflict(TCollector? customMetric)
    {
        // We always try to add an "endpoint" label with the endpoint routing route pattern.
        // We will only do this if nothing else has already occupied the "endpoint" label.
        // If a custom metric is used, we also skip this if it has no "endpoint" label name defined.
        //
        // The possible conflicts are:
        // * an existing route parameter mapping
        // * custom logic that defines an "endpoint" label
        //
        // In case of conflict, we let the user-defined item win.

        if (_additionalRouteParameters.Any(x => x.LabelName == HttpRequestLabelNames.Endpoint))
            return;

        if (_customLabels.Any(x => x.LabelName == HttpRequestLabelNames.Endpoint))
            return;

        if (customMetric != null && !customMetric.LabelNames.Contains(HttpRequestLabelNames.Endpoint))
            return;

        _customLabels.Add(new HttpCustomLabel(HttpRequestLabelNames.Endpoint, context =>
        {
            var endpoint = context.GetEndpoint() as RouteEndpoint;
            return endpoint?.RoutePattern.RawText ?? "";
        }));
    }

    /// <summary>
    /// Creates the metric child instance to use for measurements.
    /// </summary>
    /// <remarks>
    /// Internal for testing purposes.
    /// </remarks>
    protected internal TChild CreateChild(HttpContext context)
    {
        if (!_metric.LabelNames.Any())
            return _metric.Unlabelled;

        if (!_labelsRequireRouteData)
            return CreateChild(context, null);

        var routeData = context.Features.Get<ICapturedRouteDataFeature>()?.Values;

        // If we have captured route data, we always prefer it.
        // Otherwise, we extract new route data right now.
        if (routeData == null)
            routeData = context.GetRouteData()?.Values;

        return CreateChild(context, routeData);
    }

    protected TChild CreateChild(HttpContext context, RouteValueDictionary? routeData)
    {
        var labelValues = new string[_metric.LabelNames.Length];

        for (var i = 0; i < labelValues.Length; i++)
        {
            switch (_metric.LabelNames[i])
            {
                case HttpRequestLabelNames.Method:
                    labelValues[i] = context.Request.Method;
                    break;
                case HttpRequestLabelNames.Code:
                    labelValues[i] = _reduceStatusCodeCardinality ? Math.Floor(context.Response.StatusCode / 100.0).ToString("#xx") : context.Response.StatusCode.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    // If we get to this point it must be either:
                    if (_labelToRouteParameterMap.TryGetValue(_metric.LabelNames[i], out var parameterName))
                    {
                        // A mapped route parameter.
                        labelValues[i] = routeData?[parameterName] as string ?? string.Empty;
                    }
                    else if (_labelToValueProviderMap.TryGetValue(_metric.LabelNames[i], out var valueProvider))
                    {
                        // A custom label value provider.
                        labelValues[i] = valueProvider(context) ?? string.Empty;
                    }
                    else
                    {
                        // Something we do not have data for.
                        // This can happen if, for example, a custom metric inherits "all" the labels without reimplementing the "when do we add which label"
                        // logic that prometheus-net implements (which is an entirely reasonable design). So it might just add a "page" label when we have no
                        // page information. Instead of rejecting such custom metrics, we just leave the label value empty and carry on.
                        labelValues[i] = "";
                    }
                    break;
            }
        }

        return _metric.WithLabels(labelValues);
    }


    /// <summary>
    /// Creates the set of labels defined on the automatically created metric.
    /// </summary>
    private string[] CreateDefaultLabelSet()
    {
        return BaselineLabels
            .Concat(_additionalRouteParameters.Select(x => x.LabelName))
            .Concat(_customLabels.Select(x => x.LabelName))
            .ToArray();
    }

    /// <summary>
    /// Creates the full set of labels ALLOWED for the current metric.
    /// This may be a greater set than the labels automatically added to the default metric.
    /// </summary>
    private string[] CreateAllowedLabelSet()
    {
        return HttpRequestLabelNames.All
            .Concat(_additionalRouteParameters.Select(x => x.LabelName))
            .Concat(_customLabels.Select(x => x.LabelName))
            .Distinct() // Some builtins may also exist in the customs, with customs overwriting. That's fine.
            .ToArray();
    }

    private void ValidateMappings()
    {
        var routeParameterLabelNames = _additionalRouteParameters.Select(x => x.LabelName).ToList();

        if (routeParameterLabelNames.Distinct(StringComparer.InvariantCultureIgnoreCase).Count() != routeParameterLabelNames.Count)
            throw new ArgumentException("The set of additional route parameters to track contains multiple entries with the same label name.", nameof(HttpMetricsOptionsBase.AdditionalRouteParameters));

        if (HttpRequestLabelNames.Default.Except(routeParameterLabelNames, StringComparer.InvariantCultureIgnoreCase).Count() != HttpRequestLabelNames.Default.Length)
            throw new ArgumentException($"The set of additional route parameters to track contains an entry with a reserved label name. Reserved label names are: {string.Join(", ", HttpRequestLabelNames.Default)}");

        var customLabelNames = _customLabels.Select(x => x.LabelName).ToList();

        if (customLabelNames.Distinct(StringComparer.InvariantCultureIgnoreCase).Count() != customLabelNames.Count)
            throw new ArgumentException("The set of custom labels contains multiple entries with the same label name.", nameof(HttpMetricsOptionsBase.CustomLabels));

        if (HttpRequestLabelNames.Default.Except(customLabelNames, StringComparer.InvariantCultureIgnoreCase).Count() != HttpRequestLabelNames.Default.Length)
            throw new ArgumentException($"The set of custom labels contains an entry with a reserved label name. Reserved label names are: {string.Join(", ", HttpRequestLabelNames.Default)}");

        if (customLabelNames.Intersect(routeParameterLabelNames).Any())
            throw new ArgumentException("The set of custom labels and the set of additional route parameters contain conflicting label names.", nameof(HttpMetricsOptionsBase.CustomLabels));
    }

    private Dictionary<string, string> CreateLabelToRouteParameterMap()
    {
        var map = new Dictionary<string, string>(_additionalRouteParameters.Count + 2);

        // Defaults are hardcoded.
        map["action"] = "action";
        map["controller"] = "controller";

        // Any additional ones are merged.
        foreach (var entry in _additionalRouteParameters)
            map[entry.LabelName] = entry.ParameterName;

        return map;
    }

    private Dictionary<string, Func<HttpContext, string>> CreateLabelToValueProviderMap()
    {
        var map = new Dictionary<string, Func<HttpContext, string>>(_customLabels.Count);

        foreach (var entry in _customLabels)
            map[entry.LabelName] = entry.LabelValueProvider;

        return map;
    }

    /// <summary>
    /// Inspects the metric instance to ensure that all required labels are present.
    /// </summary>
    /// <remarks>
    /// If there are mappings to include route parameters in the labels, there must be labels defined for each such parameter.
    /// We do this automatically if we use the default metric instance but if a custom one is provided, this must be done by the caller.
    /// </remarks>
    private void ValidateAdditionalRouteParametersPresentInMetricLabelNames()
    {
        var labelNames = _additionalRouteParameters.Select(x => x.LabelName).ToList();
        var missing = labelNames.Except(_metric.LabelNames);

        if (missing.Any())
            throw new ArgumentException($"Provided custom HTTP request metric instance for {GetType().Name} is missing required labels: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Inspects the metric instance to ensure that all required labels are present.
    /// </summary>
    /// <remarks>
    /// If there are mappings to include custom labels, there must be label names defined for each such parameter.
    /// We do this automatically if we use the default metric instance but if a custom one is provided, this must be done by the caller.
    /// </remarks>
    private void ValidateCustomLabelsPresentInMetricLabelNames()
    {
        var labelNames = _customLabels.Select(x => x.LabelName).ToList();
        var missing = labelNames.Except(_metric.LabelNames);

        if (missing.Any())
            throw new ArgumentException($"Provided custom HTTP request metric instance for {GetType().Name} is missing required labels: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// If we use a custom metric, it should not have labels that are neither defaults nor additional route parameters.
    /// </summary>
    private void ValidateNoUnexpectedLabelNames()
    {
        var allowedLabels = CreateAllowedLabelSet();
        var unexpected = _metric.LabelNames.Except(allowedLabels);

        if (unexpected.Any())
            throw new ArgumentException($"Provided custom HTTP request metric instance for {GetType().Name} has some unexpected labels: {string.Join(", ", unexpected)}.");
    }
}
