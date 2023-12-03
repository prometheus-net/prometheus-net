#if NET
using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Prometheus;

/// <remarks>
/// Does NOT take ownership of the stream - caller remains the boss.
/// </remarks>
internal sealed class TextSerializer : IMetricsSerializer
{
    internal static ReadOnlySpan<byte> NewLine => [(byte)'\n'];
    internal static ReadOnlySpan<byte> Quote => [(byte)'"'];
    internal static ReadOnlySpan<byte> Equal => [(byte)'='];
    internal static ReadOnlySpan<byte> Comma => [(byte)','];
    internal static ReadOnlySpan<byte> Underscore => [(byte)'_'];
    internal static ReadOnlySpan<byte> LeftBrace => [(byte)'{'];
    internal static ReadOnlySpan<byte> RightBraceSpace => [(byte)'}', (byte)' '];
    internal static ReadOnlySpan<byte> Space => [(byte)' '];
    internal static ReadOnlySpan<byte> SpaceHashSpaceLeftBrace => [(byte)' ', (byte)'#', (byte)' ', (byte)'{'];
    internal static ReadOnlySpan<byte> PositiveInfinity => [(byte)'+', (byte)'I', (byte)'n', (byte)'f'];
    internal static ReadOnlySpan<byte> NegativeInfinity => [(byte)'-', (byte)'I', (byte)'n', (byte)'f'];
    internal static ReadOnlySpan<byte> NotANumber => [(byte)'N', (byte)'a', (byte)'N'];
    internal static ReadOnlySpan<byte> DotZero => [(byte)'.', (byte)'0'];
    internal static ReadOnlySpan<byte> FloatPositiveOne => [(byte)'1', (byte)'.', (byte)'0'];
    internal static ReadOnlySpan<byte> FloatZero => [(byte)'0', (byte)'.', (byte)'0'];
    internal static ReadOnlySpan<byte> FloatNegativeOne => [(byte)'-', (byte)'1', (byte)'.', (byte)'0'];
    internal static ReadOnlySpan<byte> IntPositiveOne => [(byte)'1'];
    internal static ReadOnlySpan<byte> IntZero => [(byte)'0'];
    internal static ReadOnlySpan<byte> IntNegativeOne => [(byte)'-', (byte)'1'];
    internal static ReadOnlySpan<byte> HashHelpSpace => [(byte)'#', (byte)' ', (byte)'H', (byte)'E', (byte)'L', (byte)'P', (byte)' '];
    internal static ReadOnlySpan<byte> NewlineHashTypeSpace => [(byte)'\n', (byte)'#', (byte)' ', (byte)'T', (byte)'Y', (byte)'P', (byte)'E', (byte)' '];

    internal static readonly byte[] UnknownBytes = "unknown"u8.ToArray();
    internal static readonly byte[] EofNewLineBytes = [(byte)'#', (byte)' ', (byte)'E', (byte)'O', (byte)'F', (byte)'\n'];
    internal static readonly byte[] PositiveInfinityBytes = [(byte)'+', (byte)'I', (byte)'n', (byte)'f'];

    internal static readonly Dictionary<MetricType, byte[]> MetricTypeToBytes = new()
    {
        { MetricType.Gauge, "gauge"u8.ToArray() },
        { MetricType.Counter, "counter"u8.ToArray() },
        { MetricType.Histogram, "histogram"u8.ToArray() },
        { MetricType.Summary, "summary"u8.ToArray() },
    };

    private static readonly char[] DotEChar = ['.', 'e'];

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
    private static Stream AddStreamBuffering(Stream inner)
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
        var bufferLength = MeasureFamilyDeclarationLength(name, nameBytes, helpBytes, type, typeBytes);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferLength);

        try
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
                    typeBytes = UnknownBytes; // if the total prefix is missing the _total prefix it is out of spec
                }
            }

            var position = 0;
            AppendToBufferAndIncrementPosition(HashHelpSpace, buffer, ref position);
            AppendToBufferAndIncrementPosition(nameBytes.AsSpan(0, nameLen), buffer, ref position);
            // The space after the name in "HELP" is mandatory as per ABNF, even if there is no help text.
            AppendToBufferAndIncrementPosition(Space, buffer, ref position);
            if (helpBytes.Length > 0)
            {
                AppendToBufferAndIncrementPosition(helpBytes, buffer, ref position);
            }
            AppendToBufferAndIncrementPosition(NewlineHashTypeSpace, buffer, ref position);
            AppendToBufferAndIncrementPosition(nameBytes.AsSpan(0, nameLen), buffer, ref position);
            AppendToBufferAndIncrementPosition(Space, buffer, ref position);
            AppendToBufferAndIncrementPosition(typeBytes, buffer, ref position);
            AppendToBufferAndIncrementPosition(NewLine, buffer, ref position);

            await _stream.Value.WriteAsync(buffer.AsMemory(0, position), cancel);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public int MeasureFamilyDeclarationLength(string name, byte[] nameBytes, byte[] helpBytes, MetricType type, byte[] typeBytes)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        var length = 0;

        var nameLen = nameBytes.Length;

        if (_expositionFormat == ExpositionFormat.OpenMetricsText && type == MetricType.Counter)
        {
            if (name.EndsWith("_total"))
            {
                nameLen -= 6; // in OpenMetrics the counter name does not include the _total prefix.
            }
            else
            {
                typeBytes = UnknownBytes; // if the total prefix is missing the _total prefix it is out of spec
            }
        }

        length += HashHelpSpace.Length;
        length += nameLen;
        // The space after the name in "HELP" is mandatory as per ABNF, even if there is no help text.
        length += Space.Length;
        length += helpBytes.Length;
        length += NewlineHashTypeSpace.Length;
        length += nameLen;
        length += Space.Length;
        length += typeBytes.Length;
        length += NewLine.Length;

        return length;
    }

    public async ValueTask WriteEnd(CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
            await _stream.Value.WriteAsync(EofNewLineBytes, cancel);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        double value, ObservedExemplar exemplar, byte[]? suffix, CancellationToken cancel)
    {
        // This is a max length because we do not know ahead of time how many bytes the actual value will consume.
        var bufferMaxLength = MeasureIdentifierPartLength(name, flattenedLabels, canonicalLabel, suffix) + MeasureValueMaxLength(value) + NewLine.Length;

        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
            bufferMaxLength += MeasureExemplarMaxLength(exemplar);

        var buffer = ArrayPool<byte>.Shared.Rent(bufferMaxLength);

        try
        {
            var position = WriteIdentifierPart(buffer, name, flattenedLabels, canonicalLabel, suffix);

            position += WriteValue(buffer.AsSpan(position..), value);

            if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
            {
                position += WriteExemplar(buffer.AsSpan(position..), exemplar);
            }

            AppendToBufferAndIncrementPosition(NewLine, buffer, ref position);

            ValidateBufferMaxLengthAndPosition(bufferMaxLength, position);

            await _stream.Value.WriteAsync(buffer.AsMemory(0, position), cancel);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        long value, ObservedExemplar exemplar, byte[]? suffix, CancellationToken cancel)
    {
        // This is a max length because we do not know ahead of time how many bytes the actual value will consume.
        var bufferMaxLength = MeasureIdentifierPartLength(name, flattenedLabels, canonicalLabel, suffix) + MeasureValueMaxLength(value) + NewLine.Length;

        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
            bufferMaxLength += MeasureExemplarMaxLength(exemplar);

        var buffer = ArrayPool<byte>.Shared.Rent(bufferMaxLength);

        try
        {
            var position = WriteIdentifierPart(buffer, name, flattenedLabels, canonicalLabel, suffix);

            position += WriteValue(buffer.AsSpan(position..), value);

            if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
            {
                position += WriteExemplar(buffer.AsSpan(position..), exemplar);
            }

            AppendToBufferAndIncrementPosition(NewLine, buffer, ref position);

            ValidateBufferMaxLengthAndPosition(bufferMaxLength, position);

            await _stream.Value.WriteAsync(buffer.AsMemory(0, position), cancel);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private int WriteExemplar(Span<byte> buffer, ObservedExemplar exemplar)
    {
        var position = 0;

        AppendToBufferAndIncrementPosition(SpaceHashSpaceLeftBrace, buffer, ref position);

        for (var i = 0; i < exemplar.Labels!.Length; i++)
        {
            if (i > 0)
                AppendToBufferAndIncrementPosition(Comma, buffer, ref position);

            ref var labelPair = ref exemplar.Labels[i];
            position += WriteExemplarLabel(buffer[position..], labelPair.KeyBytes, labelPair.Value);
        }

        AppendToBufferAndIncrementPosition(RightBraceSpace, buffer, ref position);
        position += WriteValue(buffer[position..], exemplar.Value);
        AppendToBufferAndIncrementPosition(Space, buffer, ref position);
        position += WriteValue(buffer[position..], exemplar.Timestamp);

        return position;
    }

    private int MeasureExemplarMaxLength(ObservedExemplar exemplar)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        var length = 0;

        length += SpaceHashSpaceLeftBrace.Length;

        for (var i = 0; i < exemplar.Labels!.Length; i++)
        {
            if (i > 0)
                length += Comma.Length;

            ref var labelPair = ref exemplar.Labels[i];
            length += MeasureExemplarLabelMaxLength(labelPair.KeyBytes, labelPair.Value);
        }

        length += RightBraceSpace.Length;
        length += MeasureValueMaxLength(exemplar.Value);
        length += Space.Length;
        length += MeasureValueMaxLength(exemplar.Timestamp);

        return length;
    }

    private static int WriteExemplarLabel(Span<byte> buffer, byte[] label, string value)
    {
        var position = 0;

        AppendToBufferAndIncrementPosition(label, buffer, ref position);
        AppendToBufferAndIncrementPosition(Equal, buffer, ref position);
        AppendToBufferAndIncrementPosition(Quote, buffer, ref position);
        position += PrometheusConstants.ExemplarEncoding.GetBytes(value.AsSpan(), buffer[position..]);
        AppendToBufferAndIncrementPosition(Quote, buffer, ref position);

        return position;
    }

    private static int MeasureExemplarLabelMaxLength(byte[] label, string value)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        var length = 0;

        length += label.Length;
        length += Equal.Length;
        length += Quote.Length;
        length += PrometheusConstants.ExemplarEncoding.GetMaxByteCount(value.Length);
        length += Quote.Length;

        return length;
    }

    private int WriteValue(Span<byte> buffer, double value)
    {
        var position = 0;

        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    AppendToBufferAndIncrementPosition(FloatZero, buffer, ref position);
                    return position;
                case 1:
                    AppendToBufferAndIncrementPosition(FloatPositiveOne, buffer, ref position);
                    return position;
                case -1:
                    AppendToBufferAndIncrementPosition(FloatNegativeOne, buffer, ref position);
                    return position;
                case double.PositiveInfinity:
                    AppendToBufferAndIncrementPosition(PositiveInfinity, buffer, ref position);
                    return position;
                case double.NegativeInfinity:
                    AppendToBufferAndIncrementPosition(NegativeInfinity, buffer, ref position);
                    return position;
                case double.NaN:
                    AppendToBufferAndIncrementPosition(NotANumber, buffer, ref position);
                    return position;
            }
        }

        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        if (!value.TryFormat(_stringCharsBuffer, out var charsWritten, "g", CultureInfo.InvariantCulture))
            throw new Exception("Failed to encode floating point value as string.");

        var encodedBytes = PrometheusConstants.ExportEncoding.GetBytes(_stringCharsBuffer, 0, charsWritten, _stringBytesBuffer, 0);
        AppendToBufferAndIncrementPosition(_stringBytesBuffer.AsSpan(0, encodedBytes), buffer, ref position);

        // In certain places (e.g. "le" label) we need floating point values to actually have the decimal point in them for OpenMetrics.
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && RequiresOpenMetricsDotZero(_stringCharsBuffer, charsWritten))
            AppendToBufferAndIncrementPosition(DotZero, buffer, ref position);

        return position;
    }

    static bool RequiresOpenMetricsDotZero(char[] buffer, int length)
    {
        return buffer.AsSpan(0..length).IndexOfAny(DotEChar) == -1; /* did not contain .|e, so needs a .0 to turn it into a floating-point value */
    }

    private int MeasureValueMaxLength(double value)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    return FloatZero.Length;
                case 1:
                    return FloatPositiveOne.Length;
                case -1:
                    return FloatNegativeOne.Length;
                case double.PositiveInfinity:
                    return PositiveInfinity.Length;
                case double.NegativeInfinity:
                    return NegativeInfinity.Length;
                case double.NaN:
                    return NotANumber.Length;
            }
        }

        // We do not want to spend time formatting the value just to measure the length and throw away the result.
        // Therefore we just consider the max length and return it. The max length is just the length of the value-encoding buffer.
        return _stringBytesBuffer.Length;
    }

    private int WriteValue(Span<byte> buffer, long value)
    {
        var position = 0;

        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    AppendToBufferAndIncrementPosition(IntZero, buffer, ref position);
                    return position;
                case 1:
                    AppendToBufferAndIncrementPosition(IntPositiveOne, buffer, ref position);
                    return position;
                case -1:
                    AppendToBufferAndIncrementPosition(IntNegativeOne, buffer, ref position);
                    return position;
            }
        }

        if (!value.TryFormat(_stringCharsBuffer, out var charsWritten, "D", CultureInfo.InvariantCulture))
            throw new Exception("Failed to encode integer value as string.");

        var encodedBytes = PrometheusConstants.ExportEncoding.GetBytes(_stringCharsBuffer, 0, charsWritten, _stringBytesBuffer, 0);
        AppendToBufferAndIncrementPosition(_stringBytesBuffer.AsSpan(0, encodedBytes), buffer, ref position);

        return position;
    }

    private int MeasureValueMaxLength(long value)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    return IntZero.Length;
                case 1:
                    return IntPositiveOne.Length;
                case -1:
                    return IntNegativeOne.Length;
            }
        }

        // We do not want to spend time formatting the value just to measure the length and throw away the result.
        // Therefore we just consider the max length and return it. The max length is just the length of the value-encoding buffer.
        return _stringBytesBuffer.Length;
    }

    // Reuse a buffer to do the serialization and UTF-8 encoding.
    // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
    private readonly char[] _stringCharsBuffer = new char[32];
    private readonly byte[] _stringBytesBuffer = new byte[32];

    private readonly ExpositionFormat _expositionFormat;

    private static void AppendToBufferAndIncrementPosition(ReadOnlySpan<byte> from, Span<byte> to, ref int position)
    {
        from.CopyTo(to[position..]);
        position += from.Length;
    }
    
    private static void ValidateBufferLengthAndPosition(int bufferLength, int position)
    {
        if (position != bufferLength)
            throw new Exception("Internal error: counting the same bytes twice got us a different value.");
    }

    private static void ValidateBufferMaxLengthAndPosition(int bufferMaxLength, int position)
    {
        if (position > bufferMaxLength)
            throw new Exception("Internal error: counting the same bytes twice got us a different value.");
    }

    /// <summary>
    /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
    /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
    /// Note: Terminates with a SPACE
    /// </summary>
    private int WriteIdentifierPart(Span<byte> buffer, byte[] name, byte[] flattenedLabels, CanonicalLabel extraLabel, byte[]? suffix = null)
    {
        var position = 0;

        AppendToBufferAndIncrementPosition(name, buffer, ref position);

        if (suffix != null && suffix.Length > 0)
        {
            AppendToBufferAndIncrementPosition(Underscore, buffer, ref position);
            AppendToBufferAndIncrementPosition(suffix, buffer, ref position);
        }

        if (flattenedLabels.Length > 0 || extraLabel.IsNotEmpty)
        {
            AppendToBufferAndIncrementPosition(LeftBrace, buffer, ref position);
            if (flattenedLabels.Length > 0)
            {
                AppendToBufferAndIncrementPosition(flattenedLabels, buffer, ref position);
            }

            // Extra labels go to the end (i.e. they are deepest to inherit from).
            if (extraLabel.IsNotEmpty)
            {
                if (flattenedLabels.Length > 0)
                {
                    AppendToBufferAndIncrementPosition(Comma, buffer, ref position);
                }

                AppendToBufferAndIncrementPosition(extraLabel.Name, buffer, ref position);
                AppendToBufferAndIncrementPosition(Equal, buffer, ref position);
                AppendToBufferAndIncrementPosition(Quote, buffer, ref position);

                if (_expositionFormat == ExpositionFormat.OpenMetricsText)
                    AppendToBufferAndIncrementPosition(extraLabel.OpenMetrics, buffer, ref position);
                else
                    AppendToBufferAndIncrementPosition(extraLabel.Prometheus, buffer, ref position);

                AppendToBufferAndIncrementPosition(Quote, buffer, ref position);
            }

            AppendToBufferAndIncrementPosition(RightBraceSpace, buffer, ref position);
        }
        else
        {
            AppendToBufferAndIncrementPosition(Space, buffer, ref position);
        }

        return position;
    }

    private int MeasureIdentifierPartLength(byte[] name, byte[] flattenedLabels, CanonicalLabel extraLabel, byte[]? suffix = null)
    {
        // We mirror the logic in the Write() call but just measure how many bytes of buffer we need.
        var length = 0;

        length += name.Length;

        if (suffix != null && suffix.Length > 0)
        {
            length += Underscore.Length;
            length += suffix.Length;
        }

        if (flattenedLabels.Length > 0 || extraLabel.IsNotEmpty)
        {
            length += LeftBrace.Length;
            if (flattenedLabels.Length > 0)
            {
                length += flattenedLabels.Length;
            }

            // Extra labels go to the end (i.e. they are deepest to inherit from).
            if (extraLabel.IsNotEmpty)
            {
                if (flattenedLabels.Length > 0)
                {
                    length += Comma.Length;
                }

                length += extraLabel.Name.Length;
                length += Equal.Length;
                length += Quote.Length;

                if (_expositionFormat == ExpositionFormat.OpenMetricsText)
                    length += extraLabel.OpenMetrics.Length;
                else
                    length += extraLabel.Prometheus.Length;

                length += Quote.Length;
            }

            length += RightBraceSpace.Length;
        }
        else
        {
            length += Space.Length;
        }

        return length;
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

            DotZero.CopyTo(openMetricsBytes.AsSpan(prometheusByteCount));
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