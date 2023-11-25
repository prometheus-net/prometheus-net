#if NET
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Prometheus;

/// <remarks>
/// Does NOT take ownership of the stream - caller remains the boss.
/// </remarks>
internal sealed class TextSerializer : IMetricsSerializer
{
    internal static readonly ReadOnlyMemory<byte> NewLine = new byte[] { (byte)'\n' };
    internal static readonly ReadOnlyMemory<byte> Quote = new byte[] { (byte)'"' };
    internal static readonly ReadOnlyMemory<byte> Equal = new byte[] { (byte)'=' };
    internal static readonly ReadOnlyMemory<byte> Comma = new byte[] { (byte)',' };
    internal static readonly ReadOnlyMemory<byte> Underscore = new byte[] { (byte)'_' };
    internal static readonly ReadOnlyMemory<byte> LeftBrace = new byte[] { (byte)'{' };
    internal static readonly ReadOnlyMemory<byte> RightBraceSpace = new byte[] { (byte)'}', (byte)' ' };
    internal static readonly ReadOnlyMemory<byte> Space = new byte[] { (byte)' ' };
    internal static readonly ReadOnlyMemory<byte> SpaceHashSpaceLeftBrace = new byte[] { (byte)' ', (byte)'#', (byte)' ', (byte)'{' };
    internal static readonly ReadOnlyMemory<byte> PositiveInfinity = PrometheusConstants.ExportEncoding.GetBytes("+Inf");
    internal static readonly ReadOnlyMemory<byte> NegativeInfinity = PrometheusConstants.ExportEncoding.GetBytes("-Inf");
    internal static readonly ReadOnlyMemory<byte> NotANumber = PrometheusConstants.ExportEncoding.GetBytes("NaN");
    internal static readonly ReadOnlyMemory<byte> DotZero = PrometheusConstants.ExportEncoding.GetBytes(".0");
    internal static readonly ReadOnlyMemory<byte> FloatPositiveOne = PrometheusConstants.ExportEncoding.GetBytes("1.0");
    internal static readonly ReadOnlyMemory<byte> FloatZero = PrometheusConstants.ExportEncoding.GetBytes("0.0");
    internal static readonly ReadOnlyMemory<byte> FloatNegativeOne = PrometheusConstants.ExportEncoding.GetBytes("-1.0");
    internal static readonly ReadOnlyMemory<byte> IntPositiveOne = PrometheusConstants.ExportEncoding.GetBytes("1");
    internal static readonly ReadOnlyMemory<byte> IntZero = PrometheusConstants.ExportEncoding.GetBytes("0");
    internal static readonly ReadOnlyMemory<byte> IntNegativeOne = PrometheusConstants.ExportEncoding.GetBytes("-1");
    internal static readonly ReadOnlyMemory<byte> EofNewLine = PrometheusConstants.ExportEncoding.GetBytes("# EOF\n");
    internal static readonly ReadOnlyMemory<byte> HashHelpSpace = PrometheusConstants.ExportEncoding.GetBytes("# HELP ");
    internal static readonly ReadOnlyMemory<byte> NewlineHashTypeSpace = PrometheusConstants.ExportEncoding.GetBytes("\n# TYPE ");
    internal static readonly byte[] Unknown = PrometheusConstants.ExportEncoding.GetBytes("unknown");

    internal static readonly byte[] PositiveInfinityBytes = PrometheusConstants.ExportEncoding.GetBytes("+Inf");

    internal static readonly Dictionary<MetricType, byte[]> MetricTypeToBytes = new()
    {
        { MetricType.Gauge, PrometheusConstants.ExportEncoding.GetBytes("gauge") },
        { MetricType.Counter, PrometheusConstants.ExportEncoding.GetBytes("counter") },
        { MetricType.Histogram, PrometheusConstants.ExportEncoding.GetBytes("histogram") },
        { MetricType.Summary, PrometheusConstants.ExportEncoding.GetBytes("summary") },
    };

    private static readonly char[] DotEChar = { '.', 'e' };

    public TextSerializer(Stream stream, ExpositionFormat fmt = ExpositionFormat.PrometheusText)
    {
        _expositionFormat = fmt;
        _stream = new Lazy<Stream>(() => AddStreamBuffering(stream));
    }

    // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
    public TextSerializer(Func<Stream> streamFactory,
        ExpositionFormat fmt = ExpositionFormat.PrometheusText)
    {
        _expositionFormat = fmt;
        _stream = new Lazy<Stream>(() => AddStreamBuffering(streamFactory()));
    }

    /// <summary>
    /// Ensures that writes to the stream are buffered, meaning we do not emit individual "write 1 byte" calls to the stream.
    /// This has been rumored by some users to be relevant in their scenarios (though never with solid evidence or repro steps).
    /// However, we can easily simulate this via the serialization benchmark through named pipes - they are super slow if writing
    /// individual characters. It is a reasonable assumption that this limitation is also true elsewhere, at least on some OS/platform.
    /// </summary>
    private Stream AddStreamBuffering(Stream inner)
    {
        return new BufferedStream(inner, bufferSize: 16 * 1024);
    }

    public async Task FlushAsync(CancellationToken cancel)
    {
        // If we never opened the stream, we don't touch it on flush.
        if (!_stream.IsValueCreated)
            return;

        await _stream.Value.FlushAsync(cancel);
    }

    private readonly Lazy<Stream> _stream;

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask WriteFamilyDeclarationAsync(string name, byte[] nameBytes, byte[] helpBytes, MetricType type,
        byte[] typeBytes, CancellationToken cancel)
    {
        var nameLen = nameBytes.Length;
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && type == MetricType.Counter)
        {
            if (name.EndsWith("_total"))
            {
                nameLen -= 6; // in OpenMetrics the counter name does not include the _total prefix.
            }
            else
            {
                typeBytes = Unknown; // if the total prefix is missing the _total prefix it is out of spec
            }
        }

        await _stream.Value.WriteAsync(HashHelpSpace, cancel);
        await _stream.Value.WriteAsync(nameBytes.AsMemory(0, nameLen), cancel);
        // The space after the name in "HELP" is mandatory as per ABNF, even if there is no help text.
        await _stream.Value.WriteAsync(Space, cancel);
        if (helpBytes.Length > 0)
        {
            await _stream.Value.WriteAsync(helpBytes, cancel);
        }
        await _stream.Value.WriteAsync(NewlineHashTypeSpace, cancel);
        await _stream.Value.WriteAsync(nameBytes.AsMemory(0, nameLen), cancel);
        await _stream.Value.WriteAsync(Space, cancel);
        await _stream.Value.WriteAsync(typeBytes, cancel);
        await _stream.Value.WriteAsync(NewLine, cancel);
    }

    public async ValueTask WriteEnd(CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
            await _stream.Value.WriteAsync(EofNewLine, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        CancellationToken cancel, double value, ObservedExemplar exemplar, byte[]? suffix = null)
    {
        await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, suffix);

        await WriteValue(value, cancel);
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
        {
            await WriteExemplarAsync(cancel, exemplar);
        }

        await _stream.Value.WriteAsync(NewLine, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        CancellationToken cancel, long value, ObservedExemplar exemplar, byte[]? suffix = null)
    {
        await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, suffix);

        await WriteValue(value, cancel);
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
        {
            await WriteExemplarAsync(cancel, exemplar);
        }

        await _stream.Value.WriteAsync(NewLine, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteExemplarAsync(CancellationToken cancel, ObservedExemplar exemplar)
    {
        await _stream.Value.WriteAsync(SpaceHashSpaceLeftBrace, cancel);
        for (var i = 0; i < exemplar.Labels!.Length; i++)
        {
            if (i > 0)
                await _stream.Value.WriteAsync(Comma, cancel);
            await WriteLabel(exemplar.Labels!.Buffer[i].KeyBytes, exemplar.Labels!.Buffer[i].ValueBytes, cancel);
        }

        await _stream.Value.WriteAsync(RightBraceSpace, cancel);
        await WriteValue(exemplar.Value, cancel);
        await _stream.Value.WriteAsync(Space, cancel);
        await WriteValue(exemplar.Timestamp, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteLabel(byte[] label, byte[] value, CancellationToken cancel)
    {
        await _stream.Value.WriteAsync(label, cancel);
        await _stream.Value.WriteAsync(Equal, cancel);
        await _stream.Value.WriteAsync(Quote, cancel);
        await _stream.Value.WriteAsync(value, cancel);
        await _stream.Value.WriteAsync(Quote, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteValue(double value, CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    await _stream.Value.WriteAsync(FloatZero, cancel);
                    return;
                case 1:
                    await _stream.Value.WriteAsync(FloatPositiveOne, cancel);
                    return;
                case -1:
                    await _stream.Value.WriteAsync(FloatNegativeOne, cancel);
                    return;
                case double.PositiveInfinity:
                    await _stream.Value.WriteAsync(PositiveInfinity, cancel);
                    return;
                case double.NegativeInfinity:
                    await _stream.Value.WriteAsync(NegativeInfinity, cancel);
                    return;
                case double.NaN:
                    await _stream.Value.WriteAsync(NotANumber, cancel);
                    return;
            }
        }

        static bool RequiresDotZero(char[] buffer, int length)
        {
            return buffer.AsSpan(0..length).IndexOfAny(DotEChar) == -1; /* did not contain .|e */
        }

        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        if (!value.TryFormat(_stringCharsBuffer, out var charsWritten, "g", CultureInfo.InvariantCulture))
            throw new Exception("Failed to encode floating point value as string.");

        var encodedBytes = PrometheusConstants.ExportEncoding.GetBytes(_stringCharsBuffer, 0, charsWritten, _stringBytesBuffer, 0);
        await _stream.Value.WriteAsync(_stringBytesBuffer.AsMemory(0, encodedBytes), cancel);

        // In certain places (e.g. "le" label) we need floating point values to actually have the decimal point in them for OpenMetrics.
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && RequiresDotZero(_stringCharsBuffer, charsWritten))
            await _stream.Value.WriteAsync(DotZero, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteValue(long value, CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    await _stream.Value.WriteAsync(IntZero, cancel);
                    return;
                case 1:
                    await _stream.Value.WriteAsync(IntPositiveOne, cancel);
                    return;
                case -1:
                    await _stream.Value.WriteAsync(IntNegativeOne, cancel);
                    return;
            }
        }

        if (!value.TryFormat(_stringCharsBuffer, out var charsWritten, "D", CultureInfo.InvariantCulture))
            throw new Exception("Failed to encode integer value as string.");

        var encodedBytes = PrometheusConstants.ExportEncoding.GetBytes(_stringCharsBuffer, 0, charsWritten, _stringBytesBuffer, 0);
        await _stream.Value.WriteAsync(_stringBytesBuffer.AsMemory(0, encodedBytes), cancel);
    }

    // Reuse a buffer to do the serialization and UTF-8 encoding.
    // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
    private readonly char[] _stringCharsBuffer = new char[32];
    private readonly byte[] _stringBytesBuffer = new byte[32];

    private readonly ExpositionFormat _expositionFormat;

    /// <summary>
    /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
    /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
    /// Note: Terminates with a SPACE
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteIdentifierPartAsync(byte[] name, byte[] flattenedLabels, CancellationToken cancel,
        CanonicalLabel canonicalLabel, byte[]? suffix = null)
    {
        await _stream.Value.WriteAsync(name, cancel);
        if (suffix != null && suffix.Length > 0)
        {
            await _stream.Value.WriteAsync(Underscore, cancel);
            await _stream.Value.WriteAsync(suffix, cancel);
        }

        if (flattenedLabels.Length > 0 || canonicalLabel.IsNotEmpty)
        {
            await _stream.Value.WriteAsync(LeftBrace, cancel);
            if (flattenedLabels.Length > 0)
            {
                await _stream.Value.WriteAsync(flattenedLabels, cancel);
            }

            // Extra labels go to the end (i.e. they are deepest to inherit from).
            if (canonicalLabel.IsNotEmpty)
            {
                if (flattenedLabels.Length > 0)
                {
                    await _stream.Value.WriteAsync(Comma, cancel);
                }

                await _stream.Value.WriteAsync(canonicalLabel.Name.AsMemory(0, canonicalLabel.Name.Length), cancel);
                await _stream.Value.WriteAsync(Equal, cancel);
                await _stream.Value.WriteAsync(Quote, cancel);
                if (_expositionFormat == ExpositionFormat.OpenMetricsText)
                    await _stream.Value.WriteAsync(canonicalLabel.OpenMetrics.AsMemory(0, canonicalLabel.OpenMetrics.Length), cancel);
                else
                    await _stream.Value.WriteAsync(canonicalLabel.Prometheus.AsMemory(0, canonicalLabel.Prometheus.Length), cancel);
                await _stream.Value.WriteAsync(Quote, cancel);
            }

            await _stream.Value.WriteAsync(RightBraceSpace, cancel);
        }
        else
        {
            await _stream.Value.WriteAsync(Space, cancel);
        }
    }

    /// <summary>
    /// Encode the special variable in regular Prometheus form and also return a OpenMetrics variant, these can be
    /// the same.
    /// see: https://github.com/OpenObservability/OpenMetrics/blob/main/specification/OpenMetrics.md#considerations-canonical-numbers
    /// </summary>
    internal static CanonicalLabel EncodeValueAsCanonicalLabel(byte[] name, double value)
    {
        if (double.IsPositiveInfinity(value))
            return new CanonicalLabel(name, PositiveInfinityBytes, PositiveInfinityBytes);

        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        Span<char> buffer = stackalloc char[32];

        if (!value.TryFormat(buffer, out var charsWritten, "g", CultureInfo.InvariantCulture))
            throw new Exception("Failed to encode floating point value as string.");

        var prometheusChars = buffer[0..charsWritten];

        var prometheusByteCount = PrometheusConstants.ExportEncoding.GetByteCount(prometheusChars);
        var prometheusBytes = new byte[prometheusByteCount];

        if (PrometheusConstants.ExportEncoding.GetBytes(prometheusChars, prometheusBytes) != prometheusByteCount)
            throw new Exception("Internal error: counting the same bytes twice got us a different value.");

        var openMetricsByteCount = prometheusByteCount;
        byte[] openMetricsBytes;

        // Identify whether the written characters are expressed as floating-point, by checking for presence of the 'e' or '.' characters.
        if (prometheusChars.IndexOfAny(DotEChar) == -1)
        {
            // Prometheus defaults to integer-formatting without a decimal point, if possible.
            // OpenMetrics requires labels containing numeric values to be expressed in floating point format.
            // If all we find is an integer, we add a ".0" to the end to make it a floating point value.
            openMetricsByteCount += 2;

            openMetricsBytes = new byte[openMetricsByteCount];
            Array.Copy(prometheusBytes, openMetricsBytes, prometheusByteCount);

            DotZero.CopyTo(openMetricsBytes.AsMemory(prometheusByteCount));
        }
        else
        {
            // It is already a floating-point value in Prometheus representation - reuse same bytes for OpenMetrics.
            openMetricsBytes = prometheusBytes;
        }

        return new CanonicalLabel(name, prometheusBytes, openMetricsBytes);
    }
}
#endif