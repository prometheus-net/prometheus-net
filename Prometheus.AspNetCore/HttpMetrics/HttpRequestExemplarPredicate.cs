using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics;

public delegate bool HttpRequestExemplarPredicate(HttpContext context);
