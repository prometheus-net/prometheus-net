using Prometheus;

namespace tester
{
    /// <summary>
    /// This is a quick and dirty abstraction that allows the metrics-serving functionality of the tester
    /// to be swapped out by changing only a single line of code. It facilitates easy manual testing of different scenarios.
    /// </summary>
    abstract class Tester
    {
        public virtual void OnStart()
        {
        }

        /// <summary>
        /// Called when it is time to observe the exported metrics and report them to the user.
        /// </summary>
        public virtual void OnTimeToObserveMetrics()
        {
        }

        public virtual void OnEnd()
        {
        }

        /// <summary>
        /// Start/Stop are called on the metric server at the appropriate moments.
        /// This may return null if the mechanism under test does not use IMetricTester method of registration.
        /// </summary>
        public abstract IMetricServer InitializeMetricServer();
    }
}
