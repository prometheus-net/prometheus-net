using Prometheus;

namespace Sample.NetStandard;

public static class ImportantProcess
{
    public static void Start()
    {
        _ = Task.Run(async delegate
        {
            while (true)
            {
                ImportantCounter.Inc();

                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }
        });
    }

    private static readonly Counter ImportantCounter = Metrics.CreateCounter("sample_important_counter", "Counts up and up and up!");
}
