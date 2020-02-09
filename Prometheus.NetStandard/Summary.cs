using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    public sealed class Summary : Collector<Summary.Child>, ISummary
    {
        // Label that defines the quantile in a summary.
        private const string QuantileLabel = "quantile";

        /// <summary>
        /// Client library guidelines say that the summary should default to not measuring quantiles.
        /// https://prometheus.io/docs/instrumenting/writing_clientlibs/#summary
        /// </summary>
        internal static readonly QuantileEpsilonPair[] DefObjectivesArray = new QuantileEpsilonPair[0];

        // Default Summary quantile values.
        public static readonly IList<QuantileEpsilonPair> DefObjectives = new List<QuantileEpsilonPair>(DefObjectivesArray);

        // Default duration for which observations stay relevant
        public static readonly TimeSpan DefMaxAge = TimeSpan.FromMinutes(10);

        // Default number of buckets used to calculate the age of observations
        public static readonly int DefAgeBuckets = 5;

        // Standard buffer size for collecting Summary observations
        public static readonly int DefBufCap = 500;

        private readonly IReadOnlyList<QuantileEpsilonPair> _objectives;
        private readonly TimeSpan _maxAge;
        private readonly int _ageBuckets;
        private readonly int _bufCap;

        internal Summary(
            string name,
            string help,
            string[]? labelNames,
            bool suppressInitialValue = false,
            IReadOnlyList<QuantileEpsilonPair>? objectives = null,
            TimeSpan? maxAge = null,
            int? ageBuckets = null,
            int? bufCap = null)
            : base(name, help, labelNames, suppressInitialValue)
        {
            _objectives = objectives ?? DefObjectivesArray;
            _maxAge = maxAge ?? DefMaxAge;
            _ageBuckets = ageBuckets ?? DefAgeBuckets;
            _bufCap = bufCap ?? DefBufCap;

            if (_objectives.Count == 0)
                _objectives = DefObjectivesArray;

            if (_maxAge < TimeSpan.Zero)
                throw new ArgumentException($"Illegal max age {_maxAge}");

            if (_ageBuckets == 0)
                _ageBuckets = DefAgeBuckets;

            if (_bufCap == 0)
                _bufCap = DefBufCap;

            if (labelNames?.Any(_ => _ == QuantileLabel) == true)
                throw new ArgumentException($"{QuantileLabel} is a reserved label name");
        }

        private protected override Child NewChild(Labels labels, bool publish)
        {
            return new Child(this, labels, publish);
        }

        private protected override MetricType Type => MetricType.Summary;

        public sealed class Child : ChildBase, ISummary
        {
            internal Child(Summary parent, Labels labels, bool publish)
                : base(parent, labels, publish)
            {
                _objectives = parent._objectives;
                _maxAge = parent._maxAge;
                _ageBuckets = parent._ageBuckets;
                _bufCap = parent._bufCap;

                _sortedObjectives = new double[_objectives.Count];
                _hotBuf = new SampleBuffer(_bufCap);
                _coldBuf = new SampleBuffer(_bufCap);
                _streamDuration = new TimeSpan(_maxAge.Ticks / _ageBuckets);
                _headStreamExpTime = DateTime.UtcNow.Add(_streamDuration);
                _hotBufExpTime = _headStreamExpTime;

                _streams = new QuantileStream[_ageBuckets];
                for (var i = 0; i < _ageBuckets; i++)
                {
                    _streams[i] = QuantileStream.NewTargeted(_objectives);
                }

                _headStream = _streams[0];

                for (var i = 0; i < _objectives.Count; i++)
                {
                    _sortedObjectives[i] = _objectives[i].Quantile;
                }

                Array.Sort(_sortedObjectives);

                _sumIdentifier = CreateIdentifier("sum");
                _countIdentifier = CreateIdentifier("count");

                _quantileIdentifiers = new byte[_objectives.Count][];
                for (var i = 0; i < _objectives.Count; i++)
                {
                    var value = double.IsPositiveInfinity(_objectives[i].Quantile) ? "+Inf" : _objectives[i].Quantile.ToString(CultureInfo.InvariantCulture);

                    _quantileIdentifiers[i] = CreateIdentifier(null, ("quantile", value));
                }
            }

            private readonly byte[] _sumIdentifier;
            private readonly byte[] _countIdentifier;
            private readonly byte[][] _quantileIdentifiers;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
            {
                // We output sum.
                // We output count.
                // We output quantiles.

                var now = DateTime.UtcNow;

                double count;
                double sum;
                var values = new List<(double quantile, double value)>(_objectives.Count);

                lock (_bufLock)
                {
                    lock (_lock)
                    {
                        // Swap bufs even if hotBuf is empty to set new hotBufExpTime.
                        SwapBufs(now);
                        FlushColdBuf();

                        count = _count;
                        sum = _sum;

                        for (var i = 0; i < _sortedObjectives.Length; i++)
                        {
                            var quantile = _sortedObjectives[i];
                            var value = _headStream.Count == 0 ? double.NaN : _headStream.Query(quantile);

                            values.Add((quantile, value));
                        }
                    }
                }

                await serializer.WriteMetricAsync(_sumIdentifier, sum, cancel);
                await serializer.WriteMetricAsync(_countIdentifier, count, cancel);

                for (var i = 0; i < values.Count; i++)
                    await serializer.WriteMetricAsync(_quantileIdentifiers[i], values[i].value, cancel);
            }

            // Objectives defines the quantile rank estimates with their respective
            // absolute error. If Objectives[q] = e, then the value reported
            // for q will be the φ-quantile value for some φ between q-e and q+e.
            // The default value is DefObjectives.
            private IReadOnlyList<QuantileEpsilonPair> _objectives = new List<QuantileEpsilonPair>();
            private double[] _sortedObjectives;
            private double _sum;
            private uint _count;
            private SampleBuffer _hotBuf;
            private SampleBuffer _coldBuf;
            private QuantileStream[] _streams;
            private TimeSpan _streamDuration;
            private QuantileStream _headStream;
            private int _headStreamIdx;
            private DateTime _headStreamExpTime;
            private DateTime _hotBufExpTime;

            // Protects hotBuf and hotBufExpTime.
            private readonly object _bufLock = new object();

            // Protects every other moving part.
            // Lock bufMtx before mtx if both are needed.
            private readonly object _lock = new object();

            // MaxAge defines the duration for which an observation stays relevant
            // for the summary. Must be positive. The default value is DefMaxAge.
            private TimeSpan _maxAge;

            // AgeBuckets is the number of buckets used to exclude observations that
            // are older than MaxAge from the summary. A higher number has a
            // resource penalty, so only increase it if the higher resolution is
            // really required. For very high observation rates, you might want to
            // reduce the number of age buckets. With only one age bucket, you will
            // effectively see a complete reset of the summary each time MaxAge has
            // passed. The default value is DefAgeBuckets.
            private int _ageBuckets;

            // BufCap defines the default sample stream buffer size.  The default
            // value of DefBufCap should suffice for most uses. If there is a need
            // to increase the value, a multiple of 500 is recommended (because that
            // is the internal buffer size of the underlying package
            // "github.com/bmizerany/perks/quantile").      
            private int _bufCap;

            public void Observe(double val)
            {
                Observe(val, DateTime.UtcNow);
            }

            /// <summary>
            /// For unit tests only
            /// </summary>
            internal void Observe(double val, DateTime now)
            {
                if (double.IsNaN(val))
                    return;

                lock (_bufLock)
                {
                    if (now > _hotBufExpTime)
                        Flush(now);

                    _hotBuf.Append(val);

                    if (_hotBuf.IsFull)
                        Flush(now);
                }

                Publish();
            }

            // Flush needs bufMtx locked.
            private void Flush(DateTime now)
            {
                lock (_lock)
                {
                    SwapBufs(now);

                    // Go version flushes on a separate goroutine, but doing this on another
                    // thread actually makes the benchmark tests slower in .net
                    FlushColdBuf();
                }
            }

            // SwapBufs needs mtx AND bufMtx locked, coldBuf must be empty.
            private void SwapBufs(DateTime now)
            {
                if (!_coldBuf.IsEmpty)
                    throw new InvalidOperationException("coldBuf is not empty");

                var temp = _hotBuf;
                _hotBuf = _coldBuf;
                _coldBuf = temp;

                // hotBuf is now empty and gets new expiration set.
                while (now > _hotBufExpTime)
                {
                    _hotBufExpTime = _hotBufExpTime.Add(_streamDuration);
                }
            }

            // FlushColdBuf needs mtx locked. 
            private void FlushColdBuf()
            {
                for (var bufIdx = 0; bufIdx < _coldBuf.Position; bufIdx++)
                {
                    var value = _coldBuf[bufIdx];

                    for (var streamIdx = 0; streamIdx < _streams.Length; streamIdx++)
                    {
                        _streams[streamIdx].Insert(value);
                    }

                    _count++;
                    _sum += value;
                }

                _coldBuf.Reset();
                MaybeRotateStreams();
            }

            // MaybeRotateStreams needs mtx AND bufMtx locked.
            private void MaybeRotateStreams()
            {
                while (!_hotBufExpTime.Equals(_headStreamExpTime))
                {
                    _headStream.Reset();
                    _headStreamIdx++;

                    if (_headStreamIdx >= _streams.Length)
                        _headStreamIdx = 0;

                    _headStream = _streams[_headStreamIdx];
                    _headStreamExpTime = _headStreamExpTime.Add(_streamDuration);
                }
            }
        }

        public void Observe(double val)
        {
            Unlabelled.Observe(val);
        }

        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();
    }
}