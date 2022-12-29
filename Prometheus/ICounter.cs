namespace Prometheus
{
    public interface ICounter : ICollectorChild
    {
        /// <summary>
        /// Increment a counter by 1.
        /// </summary>
        /// <param name="exemplar">A set of labels representing an exemplar.</param>
        void Inc(params Exemplar.LabelPair[] exemplar);
        /// <summary>
        /// Increment a counter
        /// </summary>
        /// <param name="increment">The increment.</param>
        /// <param name="exemplar">A set of labels representing an exemplar.</param>
        void Inc(double increment = 1, params Exemplar.LabelPair[] exemplar);
        void IncTo(double targetValue);
        double Value { get; }
    }
}
