namespace Prometheus
{
    /// <summary>
    /// Implemented by metric types that observe individual events with specific values.
    /// </summary>
    public interface IObserver
    {
        /// <summary>
        /// Observes an event with the given value.
        /// </summary>
        void Observe(double val);
    }
}