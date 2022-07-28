namespace Prometheus
{
    /// <summary>
    /// Implemented by metric types that observe individual events with specific values.
    /// </summary>
    public interface IObserver : ICollectorChild
    {
        /// <summary>
        /// Observes a single event with the given value.
        /// </summary>
        void Observe(double val);
    }
}