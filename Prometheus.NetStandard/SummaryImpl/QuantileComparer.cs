using Prometheus.Advanced.DataContracts;
using System.Collections.Generic;

namespace Prometheus.SummaryImpl
{
    class QuantileComparer : IComparer<Quantile>
    {
        public int Compare(Quantile x, Quantile y)
        {
            return x.quantile.CompareTo(y.quantile);
        }
    }
}
