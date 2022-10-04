using Prometheus;

var registry = Metrics.DefaultRegistry;



Console.WriteLine("Ready to start.");
Console.ReadLine();

var _counterConfiguration = new CounterConfiguration();
var DuplicateCount = 1;
var RepeatCount = 100_000;
var _metricCount = 100;

var _registry = Metrics.NewCustomRegistry();
var _factory = Metrics.WithCustomRegistry(_registry);

var _help = "arbitrary help message for metric, not relevant for benchmarking";
var _labels = new[] { "foo", "bar", "baz" };

var _metricNames = new string[_metricCount];

for (var i = 0; i < _metricCount; i++)
    _metricNames[i] = $"metric_{i:D4}";

for (var dupe = 0; dupe < DuplicateCount; dupe++)
    for (var i = 0; i < _metricCount; i++)
    {
        var metric = _factory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

        for (var repeat = 0; repeat < RepeatCount; repeat++)
            metric.WithLabels(_labels).Inc();
    }