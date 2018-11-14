namespace Prometheus
{
    public static class TimerExtensions
    {
        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of an Observer.
        /// </summary>
        public static Timer NewTimer(this IObserver observer)
        {
            return new Timer(observer);
        }
        
        public static Timer NewTimer(this IGauge gauge)
        {
            return new Timer(gauge);
        }
    }
}
