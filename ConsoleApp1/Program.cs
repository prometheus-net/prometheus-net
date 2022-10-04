using Prometheus;

var registry = Metrics.DefaultRegistry;

registry.SetStaticLabels(new Dictionary<string, string>
{
    { "registry_label_1", "value" },
    { "registry_label_2", "value" },
    { "registry_label_3", "value" },
});

var factory = Metrics.WithCustomRegistry(registry).WithLabels(new Dictionary<string, string>
{
    { "factory_label_1", "value" },
    { "factory_label_2", "value" },
    { "factory_label_3", "value" },
});

Console.WriteLine("Ready to start.");
Console.ReadLine();

var gaugeConfiguration = new GaugeConfiguration
{
    StaticLabels = new Dictionary<string, string>
    {
        { "family_label_1", "value" },
        { "family_label_2", "value" },
        { "family_label_3", "value" },
    }
};

for (var i = 0; i < 100_000_000; i++)
{
    for (var metric = 0; metric < 100; metric++)
    {
        factory.CreateGauge("foo" + metric, "bar", new string[] { "metric_label_1", "metric_label_2", "metric_label_3" }, gaugeConfiguration);
    }

    Console.WriteLine("Iteration");
}