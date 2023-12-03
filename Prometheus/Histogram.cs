using System.Numerics;
using System.Runtime.CompilerServices;

#if NET7_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace Prometheus;

/// <remarks>
/// The histogram is thread-safe but not atomic - the sum of values and total count of events
/// may not add up perfectly with bucket contents if new observations are made during a collection.
/// </remarks>
public sealed class Histogram : Collector<Histogram.Child>, IHistogram
{
    private static readonly double[] DefaultBuckets = [.005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10];

    private readonly double[] _buckets;

#if NET7_0_OR_GREATER
    // For AVX, we need to align on 32 bytes and pin the memory. This is a buffer
    // with extra items that we can "skip" when using the data, for alignment purposes.
    private readonly double[] _bucketsAlignmentBuffer;
    // How many items from the start to skip.
    private readonly int _bucketsAlignmentBufferOffset;

    private const int AvxAlignBytes = 32;
#endif

    // These labels go together with the buckets, so we do not need to allocate them for every child.
    private readonly CanonicalLabel[] _leLabels;

    private static readonly byte[] LeLabelName = "le"u8.ToArray();

    internal Histogram(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue, double[]? buckets, ExemplarBehavior exemplarBehavior)
        : base(name, help, instanceLabelNames, staticLabels, suppressInitialValue, exemplarBehavior)
    {
        if (instanceLabelNames.Contains("le"))
        {
            throw new ArgumentException("'le' is a reserved label name");
        }

        _buckets = buckets ?? DefaultBuckets;

        if (_buckets.Length == 0)
        {
            throw new ArgumentException("Histogram must have at least one bucket");
        }

        if (!double.IsPositiveInfinity(_buckets[_buckets.Length - 1]))
        {
            _buckets = [.. _buckets, double.PositiveInfinity];
        }

        for (int i = 1; i < _buckets.Length; i++)
        {
            if (_buckets[i] <= _buckets[i - 1])
            {
                throw new ArgumentException("Bucket values must be increasing");
            }
        }

        _leLabels = new CanonicalLabel[_buckets.Length];
        for (var i = 0; i < _buckets.Length; i++)
        {
            _leLabels[i] = TextSerializer.EncodeValueAsCanonicalLabel(LeLabelName, _buckets[i]);
        }

#if NET7_0_OR_GREATER
        if (Avx.IsSupported)
        {
            _bucketsAlignmentBuffer = GC.AllocateUninitializedArray<double>(_buckets.Length + (AvxAlignBytes / sizeof(double)), pinned: true);

            unsafe
            {
                var pointer = (nuint)Unsafe.AsPointer(ref _bucketsAlignmentBuffer[0]);
                var pointerTooFarByBytes = pointer % AvxAlignBytes;
                var bytesUntilNextAlignedPosition = (AvxAlignBytes - pointerTooFarByBytes) % AvxAlignBytes;

                if (bytesUntilNextAlignedPosition % sizeof(double) != 0)
                    throw new Exception("Unreachable code reached - all double[] allocations are expected to be at least 8-aligned.");

                _bucketsAlignmentBufferOffset = (int)(bytesUntilNextAlignedPosition / sizeof(double));
            }

            Array.Copy(_buckets, 0, _bucketsAlignmentBuffer, _bucketsAlignmentBufferOffset, _buckets.Length);
        }
        else
        {
            _bucketsAlignmentBuffer = [];
        }
#endif
    }

    private protected override Child NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior)
    {
        return new Child(this, instanceLabels, flattenedLabels, publish, exemplarBehavior);
    }

    public sealed class Child : ChildBase, IHistogram
    {
        internal Child(Histogram parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior)
            : base(parent, instanceLabels, flattenedLabels, publish, exemplarBehavior)
        {
            Parent = parent;

            _bucketCounts = new ThreadSafeLong[Parent._buckets.Length];

            _exemplars = new ObservedExemplar[Parent._buckets.Length];
            for (var i = 0; i < Parent._buckets.Length; i++)
            {
                _exemplars[i] = ObservedExemplar.Empty;
            }
        }

        internal new readonly Histogram Parent;

        private ThreadSafeDouble _sum = new(0.0D);
        private readonly ThreadSafeLong[] _bucketCounts;
        private static readonly byte[] SumSuffix = "sum"u8.ToArray();
        private static readonly byte[] CountSuffix = "count"u8.ToArray();
        private static readonly byte[] BucketSuffix = "bucket"u8.ToArray();
        private readonly ObservedExemplar[] _exemplars;

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
#endif
        private protected override async ValueTask CollectAndSerializeImplAsync(IMetricsSerializer serializer,
            CancellationToken cancel)
        {
            // We output sum.
            // We output count.
            // We output each bucket in order of increasing upper bound.
            await serializer.WriteMetricPointAsync(
                Parent.NameBytes,
                FlattenedLabelsBytes,
                CanonicalLabel.Empty,
                _sum.Value,
                ObservedExemplar.Empty,
                SumSuffix,
                cancel);
            await serializer.WriteMetricPointAsync(
                Parent.NameBytes,
                FlattenedLabelsBytes,
                CanonicalLabel.Empty,
                Count,
                ObservedExemplar.Empty,
                CountSuffix,
                cancel);

            var cumulativeCount = 0L;

            for (var i = 0; i < _bucketCounts.Length; i++)
            {
                var exemplar = BorrowExemplar(ref _exemplars[i]);

                cumulativeCount += _bucketCounts[i].Value;
                await serializer.WriteMetricPointAsync(
                    Parent.NameBytes,
                    FlattenedLabelsBytes,
                    Parent._leLabels[i],
                    cumulativeCount,
                    exemplar,
                    BucketSuffix,
                    cancel);

                ReturnBorrowedExemplar(ref _exemplars[i], exemplar);
            }
        }

        public double Sum => _sum.Value;

        public long Count
        {
            get
            {
                long total = 0;

                foreach (var count in _bucketCounts)
                    total += count.Value;

                return total;
            }
        }

        public void Observe(double val, Exemplar? exemplarLabels) => ObserveInternal(val, 1, exemplarLabels);

        public void Observe(double val) => Observe(val, 1);

        public void Observe(double val, long count) => ObserveInternal(val, count, null);

        private void ObserveInternal(double val, long count, Exemplar? exemplar)
        {
            if (double.IsNaN(val))
            {
                return;
            }

            exemplar ??= GetDefaultExemplar(val);

            var bucketIndex = GetBucketIndex(val);

            _bucketCounts[bucketIndex].Add(count);

            if (exemplar?.Length > 0)
                RecordExemplar(exemplar, ref _exemplars[bucketIndex], val);

            _sum.Add(val * count);

            Publish();
        }

        private int GetBucketIndex(double val)
        {
#if NET7_0_OR_GREATER
            if (Avx.IsSupported)
                return GetBucketIndexAvx(val);
#endif

            for (int i = 0; i < Parent._buckets.Length; i++)
            {
                if (val <= Parent._buckets[i])
                    return i;
            }

            throw new Exception("Unreachable code reached.");
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// AVX allows us to perform 4 comparisons at the same time when finding the right bucket to increment.
        /// The total speedup is not 4x due to various overheads but it's still 10-30% (more for wider histograms).
        /// </summary>
        private unsafe int GetBucketIndexAvx(double val)
        {
            // AVX operates on vectors of N buckets, so if the total is not divisible by N we need to check some of them manually.
            var remaining = Parent._buckets.Length % Vector256<double>.Count;

            for (int i = 0; i < Parent._buckets.Length - remaining; i += Vector256<double>.Count)
            {
                // The buckets are permanently pinned, no need to re-pin them here.
                var boundPointer = (double*)Unsafe.AsPointer(ref Parent._bucketsAlignmentBuffer[Parent._bucketsAlignmentBufferOffset + i]);
                var boundVector = Avx.LoadAlignedVector256(boundPointer);

                var valVector = Vector256.Create(val);

                var mask = Avx.CompareLessThanOrEqual(valVector, boundVector);

                // Condenses the mask vector into a 32-bit integer where one bit represents one vector element (so 1111000.. means "first 4 items true").
                var moveMask = Avx.MoveMask(mask);

                var indexInBlock = BitOperations.TrailingZeroCount(moveMask);

                if (indexInBlock == sizeof(int) * 8)
                    continue; // All bits are zero, so we did not find a match.

                return i + indexInBlock;
            }

            for (int i = Parent._buckets.Length - remaining; i < Parent._buckets.Length; i++)
            {
                if (val <= Parent._buckets[i])
                    return i;
            }

            throw new Exception("Unreachable code reached.");
        }
#endif
    }

    internal override MetricType Type => MetricType.Histogram;

    public double Sum => Unlabelled.Sum;
    public long Count => Unlabelled.Count;
    public void Observe(double val) => Unlabelled.Observe(val, 1);
    public void Observe(double val, long count) => Unlabelled.Observe(val, count);
    public void Observe(double val, Exemplar? exemplar) => Unlabelled.Observe(val, exemplar);
    public void Publish() => Unlabelled.Publish();
    public void Unpublish() => Unlabelled.Unpublish();

    // From https://github.com/prometheus/client_golang/blob/master/prometheus/histogram.go
    /// <summary>  
    ///  Creates '<paramref name="count"/>' buckets, where the lowest bucket has an
    ///  upper bound of '<paramref name="start"/>' and each following bucket's upper bound is '<paramref name="factor"/>'
    ///  times the previous bucket's upper bound.
    /// 
    ///  The function throws if '<paramref name="count"/>' is 0 or negative, if '<paramref name="start"/>' is 0 or negative,
    ///  or if '<paramref name="factor"/>' is less than or equal 1.
    /// </summary>
    /// <param name="start">The upper bound of the lowest bucket. Must be positive.</param>
    /// <param name="factor">The factor to increase the upper bound of subsequent buckets. Must be greater than 1.</param>
    /// <param name="count">The number of buckets to create. Must be positive.</param>
    public static double[] ExponentialBuckets(double start, double factor, int count)
    {
        if (count <= 0) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a positive {nameof(count)}");
        if (start <= 0) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a positive {nameof(start)}");
        if (factor <= 1) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a {nameof(factor)} greater than 1");

        // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
        // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
        // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.

        var next = (decimal)start;
        var buckets = new double[count];

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = (double)next;
            next *= (decimal)factor;
        }

        return buckets;
    }

    // From https://github.com/prometheus/client_golang/blob/master/prometheus/histogram.go
    /// <summary>  
    ///  Creates '<paramref name="count"/>' buckets, where the lowest bucket has an
    ///  upper bound of '<paramref name="start"/>' and each following bucket's upper bound is the upper bound of the
    ///  previous bucket, incremented by '<paramref name="width"/>'
    /// 
    ///  The function throws if '<paramref name="count"/>' is 0 or negative.
    /// </summary>
    /// <param name="start">The upper bound of the lowest bucket.</param>
    /// <param name="width">The width of each bucket (distance between lower and upper bound).</param>
    /// <param name="count">The number of buckets to create. Must be positive.</param>
    public static double[] LinearBuckets(double start, double width, int count)
    {
        if (count <= 0) throw new ArgumentException($"{nameof(LinearBuckets)} needs a positive {nameof(count)}");

        // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
        // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
        // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.

        var next = (decimal)start;
        var buckets = new double[count];

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = (double)next;
            next += (decimal)width;
        }

        return buckets;
    }

    /// <summary>
    /// Divides each power of 10 into N divisions.
    /// </summary>
    /// <param name="startPower">The starting range includes 10 raised to this power.</param>
    /// <param name="endPower">The ranges end with 10 raised to this power (this no longer starts a new range).</param>
    /// <param name="divisions">How many divisions to divide each range into.</param>
    /// <remarks>
    /// For example, with startPower=-1, endPower=2, divisions=4 we would get:
    /// 10^-1 == 0.1 which defines our starting range, giving buckets: 0.25, 0.5, 0.75, 1.0
    /// 10^0 == 1 which is the next range, giving buckets: 2.5, 5, 7.5, 10
    /// 10^1 == 10 which is the next range, giving buckets: 25, 50, 75, 100
    /// 10^2 == 100 which is the end and the top level of the preceding range.
    /// Giving total buckets: 0.25, 0.5, 0.75, 1.0, 2.5, 5, 7.5, 10, 25, 50, 75, 100
    /// </remarks>
    public static double[] PowersOfTenDividedBuckets(int startPower, int endPower, int divisions)
    {
        if (startPower >= endPower)
            throw new ArgumentException($"{nameof(startPower)} must be less than {nameof(endPower)}.", nameof(startPower));

        if (divisions <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(divisions)} must be a positive integer.", nameof(divisions));

        var buckets = new List<double>();

        for (var powerOfTen = startPower; powerOfTen < endPower; powerOfTen++)
        {
            // This gives us the upper bound (the start of the next range).
            var max = (decimal)Math.Pow(10, powerOfTen + 1);

            // Then we just divide it into N divisions and we are done!
            for (var division = 0; division < divisions; division++)
            {
                var bucket = max / divisions * (division + 1);

                // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
                // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
                // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.
                var candidate = (double)bucket;

                // Depending on the number of divisions, it may be that divisions from different powers overlap.
                // For example, a division into 20 would include:
                // 19th value in the 0th power: 9.5 (10/20*19=9.5)
                // 1st value in the 1st power: 5 (100/20*1 = 5)
                // To avoid this being a problem, we simply constrain all values to be increasing.
                if (buckets.Any() && buckets.Last() >= candidate)
                    continue; // Skip this one, it is not greater.

                buckets.Add(candidate);
            }
        }

        return [.. buckets];
    }

    // sum + count + buckets
    internal override int TimeseriesCount => ChildCount * (2 + _buckets.Length);
}