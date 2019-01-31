using System.Collections.Generic;

namespace Prometheus.SummaryImpl
{
    internal class QuantileComparer : IComparer<SummaryQuantileData>
    {
        public int Compare(SummaryQuantileData x, SummaryQuantileData y)
        {
            return x.Quantile.CompareTo(y.Quantile);
        }
    }
}
