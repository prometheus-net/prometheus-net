using Prometheus;
using System;
using System.Net;
using System.Threading.Tasks;

// This sample demonstrates how to integrate prometheus-net into a console app (e.g. a worker service).
// 
// NuGet packages required:
// * prometheus-net

internal static class Program
{
    public static void Main(string[] args)
    {
        // Start the metrics server on your preferred port number.
        var server = new MetricServer(port: 1234);

        try
        {
            // On .NET Framework, starting the server requires either elevation to Administrator or permission configuration.
            server.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"Failed to start metric server: {ex.Message}");
            Console.WriteLine("You may need to grant permissions to your user account if not running as Administrator:");
            Console.WriteLine("netsh http add urlacl url=http://+:1234/metrics user=DOMAIN\\user");
            return;
        }

        // Generate some sample data from fake business logic.
        var recordsProcessed = Metrics.CreateCounter("sample_records_processed_total", "Total number of records processed.");

        _ = Task.Run(async delegate
        {
            while (true)
            {
                // Pretend to process a record approximately every second, just for changing sample data.
                recordsProcessed.Inc();

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        });

        // Metrics published in this sample:
        // * built-in process metrics giving basic information about the .NET runtime (enabled by default)
        // * the sample counter defined above
        Console.WriteLine("Open http://localhost:1234/metrics in a web browser.");
        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();

        server.Stop();
    }
}