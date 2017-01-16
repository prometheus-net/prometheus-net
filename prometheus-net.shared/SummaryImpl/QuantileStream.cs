using System.Collections.Generic;

namespace Prometheus.SummaryImpl
{
    // Ported from https://github.com/beorn7/perks/blob/master/quantile/stream.go

    // Package quantile computes approximate quantiles over an unbounded data
    // stream within low memory and CPU bounds.
    //
    // A small amount of accuracy is traded to achieve the above properties.
    //
    // Multiple streams can be merged before calling Query to generate a single set
    // of results. This is meaningful when the streams represent the same type of
    // data. See Merge and Samples.
    //
    // For more detailed information about the algorithm used, see:
    //
    // Effective Computation of Biased Quantiles over Data Streams
    //
    // http://www.cs.rutgers.edu/~muthu/bquant.pdf

    delegate double Invariant(SampleStream stream, double r);

    class QuantileStream
    {
        readonly SampleStream _sampleStream;
        readonly List<Sample> _samples;
        bool _sorted;

        QuantileStream(SampleStream sampleStream, List<Sample> samples, bool sorted)
        {
            _sampleStream = sampleStream;
            _samples = samples;
            _sorted = sorted;
        }

        public static QuantileStream NewStream(Invariant invariant)
        {
            return new QuantileStream(new SampleStream(invariant), new List<Sample> {Capacity = 500}, true);
        }

        // NewLowBiased returns an initialized Stream for low-biased quantiles
        // (e.g. 0.01, 0.1, 0.5) where the needed quantiles are not known a priori, but
        // error guarantees can still be given even for the lower ranks of the data
        // distribution.
        //
        // The provided epsilon is a relative error, i.e. the true quantile of a value
        // returned by a query is guaranteed to be within (1±Epsilon)*Quantile.
        //
        // See http://www.cs.rutgers.edu/~muthu/bquant.pdf for time, space, and error
        // properties.
        public static QuantileStream NewLowBiased(double epsilon)
        {
            return NewStream((stream, r) => 2 * epsilon * r);
        }

        // NewHighBiased returns an initialized Stream for high-biased quantiles
        // (e.g. 0.01, 0.1, 0.5) where the needed quantiles are not known a priori, but
        // error guarantees can still be given even for the higher ranks of the data
        // distribution.
        //
        // The provided epsilon is a relative error, i.e. the true quantile of a value
        // returned by a query is guaranteed to be within 1-(1±Epsilon)*(1-Quantile).
        //
        // See http://www.cs.rutgers.edu/~muthu/bquant.pdf for time, space, and error
        // properties.
        public static QuantileStream NewHighBiased(double epsilon)
        {
            return NewStream((stream, r) => 2 * epsilon * (stream.N - r));
        }

        // NewTargeted returns an initialized Stream concerned with a particular set of
        // quantile values that are supplied a priori. Knowing these a priori reduces
        // space and computation time. The targets map maps the desired quantiles to
        // their absolute errors, i.e. the true quantile of a value returned by a query
        // is guaranteed to be within (Quantile±Epsilon).
        //
        // See http://www.cs.rutgers.edu/~muthu/bquant.pdf for time, space, and error properties.
        public static QuantileStream NewTargeted(IList<QuantileEpsilonPair> targets)
        {
            return NewStream((stream, r) =>
            {
                var m = double.MaxValue;

                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                
                    double f;
                    if (target.Quantile* stream.N <= r)
                        f = (2*target.Epsilon*r)/target.Quantile;
                    else
                        f = (2*target.Epsilon*(stream.N - r))/(1 - target.Quantile);

                    if (f < m)
                        m = f;
                }

                return m;
            });
        }

        public void Insert(double value)
        {
            Insert(new Sample {Value = value, Width = 1});
        }

        void Insert(Sample sample)
        {
            _samples.Add(sample);
            _sorted = false;
            if (_samples.Count == _samples.Capacity)
                Flush();
        }

        void Flush()
        {
            MaybeSort();
            _sampleStream.Merge(_samples);
            _samples.Clear();
        }

        void MaybeSort()
        {
            if (!_sorted)
            {
                _sorted = true;
                _samples.Sort( SampleComparison);
            }
        }
        static int SampleComparison(Sample lhs, Sample rhs)
        {
            return lhs.Value.CompareTo(rhs.Value);
        }

        public void Reset()
        {
            _sampleStream.Reset();
            _samples.Clear();
        }

        // Count returns the total number of samples observed in the stream since initialization.
        public int Count => _samples.Count + _sampleStream.Count;

        public int SamplesCount => _samples.Count;

        public bool Flushed => _sampleStream.SampleCount > 0;

        // Query returns the computed qth percentiles value. If s was created with
        // NewTargeted, and q is not in the set of quantiles provided a priori, Query
        // will return an unspecified result.
        public double Query(double q)
        {
            if (!Flushed)
            {
                // Fast path when there hasn't been enough data for a flush;
                // this also yields better accuracy for small sets of data.

                var l = _samples.Count;

                if (l == 0)
                    return 0;

                var i = (int) (l * q);
                if (i > 0)
                    i -= 1;

                MaybeSort();
                return _samples[i].Value;
            }

            Flush();
            return _sampleStream.Query(q);
        }
    }
}
