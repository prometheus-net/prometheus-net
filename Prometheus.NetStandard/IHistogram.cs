namespace Prometheus
{
    public interface IHistogram : IObserver
    {
        /// <summary>
        /// Observe multiple events with a given value.
        /// 
        /// Intended to support high frequency or batch processing use cases utilizing pre-aggregation.
        /// </summary>
        /// <param name="val">Measured value.</param>
        /// <param name="count">Number of observations with this value.</param>
        void Observe(double val, long count);

        /// <summary>
        /// Gets the sum of all observed events.
        /// </summary>
        double Sum { get; }

        /// <summary>
        /// Gets the count of all observed events.
        /// </summary>
        long Count { get; }
    }
}
