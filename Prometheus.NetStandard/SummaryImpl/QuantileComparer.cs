using Prometheus.DataContracts;
using System.Collections.Generic;

namespace Prometheus.SummaryImpl
{
    internal class QuantileComparer : IComparer<Quantile>
    {
        public int Compare(Quantile x, Quantile y)
        {
            return x.quantile.CompareTo(y.quantile);
        }
    }
}
