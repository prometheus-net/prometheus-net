using System;

namespace Prometheus.Tests
{
    public static class RandomExtensions
    {
        public static double NormDouble(this Random r)
        {
            var u1 = r.NextDouble();
            var u2 = r.NextDouble();

            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
