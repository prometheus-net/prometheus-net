using System.Diagnostics.Metrics;

/// <summary>
/// Sample custom metrics exported via the .NET Meters API.
/// </summary>
public static class CustomDotNetMeters
{
    public static void PublishSampleData()
    {
        // The meter object is the "container" for all the .NET metrics we will be publishing.
        var meter1 = new Meter("Foobar.Wingwang.Dingdong", "vNext");

        // Example metric: a simple counter.
        var counter1 = meter1.CreateCounter<int>("wings-wanged", "wings", "Counts the number of wings that have been wanged.");

        double nameCount = 1_000_000_000;

        double HowManyNamesAreThereInTheWorld() => nameCount++;

        var counter2 = meter1.CreateObservableCounter<double>("all-names", HowManyNamesAreThereInTheWorld, "names", "Count of how many unique names exist in the world up to this point");

        // Example metric: an observable gauge.
        IEnumerable<Measurement<double>> ObserveGrossNestsAll()
        {
            foreach (var neepitKeepit in Enumerable.Range(1, 10))
                yield return new Measurement<double>(Random.Shared.Next(800), new KeyValuePair<string, object?>("beek-beek", "yes"), new KeyValuePair<string, object?>("neepit-keepit", Random.Shared.Next(neepitKeepit)));
        }

        var observableGauge1 = meter1.CreateObservableGauge<double>("gross-nests", ObserveGrossNestsAll, "nests (gross)", "Measures the amount of nests nested (gross).");

        // Example metric: a histogram.
        var histogram1 = meter1.CreateHistogram<byte>("bytes-considered", "bytes", "Informs about all the bytes considered.");

        // .NET 7: Example metric: an up/down counter.
        var upDown1 = meter1.CreateUpDownCounter<int>("water-level", "brick-heights", "Current water level in the tank (measured in visible bricks from the midpoint).");

        // Example metric: an observable up/down counter.
        int sandLevel = 0;

        int MeasureSandLevel()
        {
            sandLevel += Random.Shared.Next(-1, 2);
            return sandLevel;
        }

        var upDown2 = meter1.CreateObservableUpDownCounter<int>("sand-level", MeasureSandLevel, "chainlinks", "Current sand level in the tank (measured in visible chain links from the midpoint).");

        // Example high cardinality metric: bytes sent per connection.
        var highCardinalityCounter1 = meter1.CreateCounter<long>("bytes-sent", "bytes", "Bytes sent per connection.");

        var activeConnections = new List<Guid>();

        // Start with 10 active connections.
        foreach (var _ in Enumerable.Range(0, 10))
            activeConnections.Add(Guid.NewGuid());

        // Dummy data generator.
        _ = Task.Run(async delegate
        {
            while (true)
            {
                if (Random.Shared.Next(10) == 0)
                    counter1.Add(1, new KeyValuePair<string, object?>("wing-type", "FlexxWing MaxxFling 3000"));

                if (Random.Shared.Next(10) == 0)
                    counter1.Add(1, new KeyValuePair<string, object?>("wing-type", "SlaxxWing 1.0"), new KeyValuePair<string, object?>("wing-version", "beta"));

                // is-faulted here conflicts with the static label of the same name and gets overwritten by the static label.
                histogram1.Record((byte)(Random.Shared.Next(256)), new KeyValuePair<string, object?>("is-faulted", true), new KeyValuePair<string, object?>("canbus_ver", "1.0"));

                // .NET 7
                upDown1.Add(Random.Shared.Next(-1, 2));

                // Add some bytes for every active connection.
                foreach (var connection in activeConnections)
                    highCardinalityCounter1.Add(Random.Shared.Next(10_000_000), new KeyValuePair<string, object?>("connection-id", connection));

                // Maybe some connection went away, maybe some was added.
                // Timeseries that stop receiving updates will disappear from prometheus-net output after a short delay (up to 10 minutes by default).
                if (Random.Shared.Next(100) == 0)
                {
                    activeConnections.RemoveAt(Random.Shared.Next(activeConnections.Count));
                    activeConnections.Add(Guid.NewGuid());
                }

                await Task.Delay(100);
            }
        });
    }
}
