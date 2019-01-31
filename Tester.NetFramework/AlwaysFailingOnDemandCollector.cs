using Prometheus;

namespace tester
{
    public sealed class AlwaysFailingOnDemandCollector : IOnDemandCollector
    {
        public void RegisterMetrics(ICollectorRegistry registry)
        {
        }

        public void UpdateMetrics()
        {
            throw new ScrapeFailedException("The scrape failed. Oh no!");
        }
    }
}
