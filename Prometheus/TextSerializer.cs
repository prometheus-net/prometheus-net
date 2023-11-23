using System.Globalization;

namespace Prometheus;

/// <remarks>
/// Does NOT take ownership of the stream - caller remains the boss.
/// </remarks>
internal sealed class TextSerializer : IMetricsSerializer
{
    private static readonly byte[] NewLine = { (byte)'\n' };
    private static readonly byte[] Quote = { (byte)'"' };
    private static readonly byte[] Equal = { (byte)'=' };
    private static readonly byte[] Comma = { (byte)',' };
    private static readonly byte[] Underscore = { (byte)'_' };
    private static readonly byte[] LeftBrace = { (byte)'{' };
    private static readonly byte[] RightBraceSpace = { (byte)'}', (byte)' ' };
    private static readonly byte[] Space = { (byte)' ' };
    private static readonly byte[] SpaceHashSpaceLeftBrace = { (byte)' ', (byte)'#', (byte)' ', (byte)'{' };
    private static readonly byte[] PositiveInfinity = PrometheusConstants.ExportEncoding.GetBytes("+Inf");
    private static readonly byte[] NegativeInfinity = PrometheusConstants.ExportEncoding.GetBytes("-Inf");
    private static readonly byte[] NotANumber = PrometheusConstants.ExportEncoding.GetBytes("NaN");
    private static readonly byte[] DotZero = PrometheusConstants.ExportEncoding.GetBytes(".0");
    private static readonly byte[] FloatPositiveOne = PrometheusConstants.ExportEncoding.GetBytes("1.0");
    private static readonly byte[] FloatZero = PrometheusConstants.ExportEncoding.GetBytes("0.0");
    private static readonly byte[] FloatNegativeOne = PrometheusConstants.ExportEncoding.GetBytes("-1.0");
    private static readonly byte[] IntPositiveOne = PrometheusConstants.ExportEncoding.GetBytes("1");
    private static readonly byte[] IntZero = PrometheusConstants.ExportEncoding.GetBytes("0");
    private static readonly byte[] IntNegativeOne = PrometheusConstants.ExportEncoding.GetBytes("-1");
    private static readonly byte[] EofNewLine = PrometheusConstants.ExportEncoding.GetBytes("# EOF\n");
    private static readonly byte[] HashHelpSpace = PrometheusConstants.ExportEncoding.GetBytes("# HELP ");
    private static readonly byte[] NewlineHashTypeSpace = PrometheusConstants.ExportEncoding.GetBytes("\n# TYPE ");
    private static readonly byte[] Unknown = PrometheusConstants.ExportEncoding.GetBytes("unknown");

    private static readonly char[] DotEChar = { '.', 'e' };

    public TextSerializer(Stream stream, ExpositionFormat fmt = ExpositionFormat.PrometheusText)
    {
        _expositionFormat = fmt;
        _stream = new Lazy<Stream>(() => stream);
    }

    // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
    public TextSerializer(Func<Stream> streamFactory,
        ExpositionFormat fmt = ExpositionFormat.PrometheusText)
    {
        _expositionFormat = fmt;
        _stream = new Lazy<Stream>(streamFactory);
    }

    public async Task FlushAsync(CancellationToken cancel)
    {
        // If we never opened the stream, we don't touch it on flush.
        if (!_stream.IsValueCreated)
            return;

        await _stream.Value.FlushAsync(cancel);
    }

    private readonly Lazy<Stream> _stream;

    public async Task WriteFamilyDeclarationAsync(string name, byte[] nameBytes, byte[] helpBytes, MetricType type,
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

        await _stream.Value.WriteAsync(HashHelpSpace, 0, HashHelpSpace.Length, cancel);
        await _stream.Value.WriteAsync(nameBytes, 0, nameLen, cancel);
        // The space after the name in "HELP" is mandatory as per ABNF, even if there is no help text.
        await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
        if (helpBytes.Length > 0)
        {
            await _stream.Value.WriteAsync(helpBytes, 0, helpBytes.Length, cancel);
        }
        await _stream.Value.WriteAsync(NewlineHashTypeSpace, 0, NewlineHashTypeSpace.Length, cancel);
        await _stream.Value.WriteAsync(nameBytes, 0, nameLen, cancel);
        await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
        await _stream.Value.WriteAsync(typeBytes, 0, typeBytes.Length, cancel);
        await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
    }

    public async Task WriteEnd(CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
            await _stream.Value.WriteAsync(EofNewLine, 0, EofNewLine.Length, cancel);
    }

    public async Task WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        CancellationToken cancel, double value, ObservedExemplar exemplar, byte[]? suffix = null)
    {
        await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, exemplar, suffix);

        await WriteValue(value, cancel);
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
        {
            await WriteExemplarAsync(cancel, exemplar);
        }

        await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
    }

    public async Task WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
        CancellationToken cancel, long value, ObservedExemplar exemplar, byte[]? suffix = null)
    {
        await WriteIdentifierPartAsync(name, flattenedLabels, cancel, canonicalLabel, exemplar, suffix);

        await WriteValue(value, cancel);
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && exemplar.IsValid)
        {
            await WriteExemplarAsync(cancel, exemplar);
        }

        await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
    }

    private async Task WriteExemplarAsync(CancellationToken cancel, ObservedExemplar exemplar)
    {
        await _stream.Value.WriteAsync(SpaceHashSpaceLeftBrace, 0, SpaceHashSpaceLeftBrace.Length, cancel);
        for (var i = 0; i < exemplar.Labels!.Length; i++)
        {
            if (i > 0)
                await _stream.Value.WriteAsync(Comma, 0, Comma.Length, cancel);
            await WriteLabel(exemplar.Labels!.Buffer[i].KeyBytes, exemplar.Labels!.Buffer[i].ValueBytes, cancel);
        }

        await _stream.Value.WriteAsync(RightBraceSpace, 0, RightBraceSpace.Length, cancel);
        await WriteValue(exemplar.Value, cancel);
        await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
        await WriteValue(exemplar.Timestamp, cancel);
    }

    private async Task WriteLabel(byte[] label, byte[] value, CancellationToken cancel)
    {
        await _stream.Value.WriteAsync(label, 0, label.Length, cancel);
        await _stream.Value.WriteAsync(Equal, 0, Equal.Length, cancel);
        await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
        await _stream.Value.WriteAsync(value, 0, value.Length, cancel);
        await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
    }

    private async Task WriteValue(double value, CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    await _stream.Value.WriteAsync(FloatZero, 0, FloatZero.Length, cancel);
                    return;
                case 1:
                    await _stream.Value.WriteAsync(FloatPositiveOne, 0, FloatPositiveOne.Length, cancel);
                    return;
                case -1:
                    await _stream.Value.WriteAsync(FloatNegativeOne, 0, FloatNegativeOne.Length, cancel);
                    return;
                case double.PositiveInfinity:
                    await _stream.Value.WriteAsync(PositiveInfinity, 0, PositiveInfinity.Length, cancel);
                    return;
                case double.NegativeInfinity:
                    await _stream.Value.WriteAsync(NegativeInfinity, 0, NegativeInfinity.Length, cancel);
                    return;
                case double.NaN:
                    await _stream.Value.WriteAsync(NotANumber, 0, NotANumber.Length, cancel);
                    return;
            }
        }

        var valueAsString = value.ToString("g", CultureInfo.InvariantCulture);

        var numBytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);
        await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);

        // In certain places (e.g. "le" label) we need floating point values to actually have the decimal point in them for OpenMetrics.
        if (_expositionFormat == ExpositionFormat.OpenMetricsText && valueAsString.IndexOfAny(DotEChar) == -1 /* did not contain .|e */)
            await _stream.Value.WriteAsync(DotZero, 0, DotZero.Length, cancel);
    }

    private async Task WriteValue(long value, CancellationToken cancel)
    {
        if (_expositionFormat == ExpositionFormat.OpenMetricsText)
        {
            switch (value)
            {
                case 0:
                    await _stream.Value.WriteAsync(IntZero, 0, IntZero.Length, cancel);
                    return;
                case 1:
                    await _stream.Value.WriteAsync(IntPositiveOne, 0, IntPositiveOne.Length, cancel);
                    return;
                case -1:
                    await _stream.Value.WriteAsync(IntNegativeOne, 0, IntNegativeOne.Length, cancel);
                    return;
            }
        }

        var valueAsString = value.ToString("D", CultureInfo.InvariantCulture);
        
        var numBytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);
        await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);
    }

    // Reuse a buffer to do the UTF-8 encoding.
    // Maybe one day also ValueStringBuilder but that would be .NET Core only.
    // https://github.com/dotnet/corefx/issues/28379
    // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
    private readonly byte[] _stringBytesBuffer = new byte[32];
    private readonly ExpositionFormat _expositionFormat;

    /// <summary>
    /// Creates a metric identifier, with an optional name postfix and an optional extra label to append to the end.
    /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
    /// Note: Terminates with a SPACE
    /// </summary>
    private async Task WriteIdentifierPartAsync(byte[] name, byte[] flattenedLabels, CancellationToken cancel,
        CanonicalLabel canonicalLabel, ObservedExemplar observedExemplar, byte[]? suffix = null)
    {
        await _stream.Value.WriteAsync(name, 0, name.Length, cancel);
        if (suffix != null && suffix.Length > 0)
        {
            await _stream.Value.WriteAsync(Underscore, 0, Underscore.Length, cancel);
            await _stream.Value.WriteAsync(suffix, 0, suffix.Length, cancel);
        }

        if (flattenedLabels.Length > 0 || canonicalLabel.IsNotEmpty)
        {
            await _stream.Value.WriteAsync(LeftBrace, 0, LeftBrace.Length, cancel);
            if (flattenedLabels.Length > 0)
            {
                await _stream.Value.WriteAsync(flattenedLabels, 0, flattenedLabels.Length, cancel);
            }

            // Extra labels go to the end (i.e. they are deepest to inherit from).
            if (canonicalLabel.IsNotEmpty)
            {
                if (flattenedLabels.Length > 0)
                {
                    await _stream.Value.WriteAsync(Comma, 0, Comma.Length, cancel);
                }

                await _stream.Value.WriteAsync(canonicalLabel.Name, 0, canonicalLabel.Name.Length, cancel);
                await _stream.Value.WriteAsync(Equal, 0, Equal.Length, cancel);
                await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
                if (_expositionFormat == ExpositionFormat.OpenMetricsText)
                    await _stream.Value.WriteAsync(
                        canonicalLabel.OpenMetrics, 0, canonicalLabel.OpenMetrics.Length, cancel);
                else
                    await _stream.Value.WriteAsync(
                        canonicalLabel.Prometheus, 0, canonicalLabel.Prometheus.Length, cancel);
                await _stream.Value.WriteAsync(Quote, 0, Quote.Length, cancel);
            }

            await _stream.Value.WriteAsync(RightBraceSpace, 0, RightBraceSpace.Length, cancel);
        }
        else
        {
            await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
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
            return new CanonicalLabel(name, PositiveInfinity, PositiveInfinity);

#if NET
        Span<char> buffer = stackalloc char[128];

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
            Array.Copy(DotZero, 0, openMetricsBytes, prometheusByteCount, DotZero.Length);
        }
        else
        {
            // It is already a floating-point value in Prometheus representation - reuse same bytes for OpenMetrics.
            openMetricsBytes = prometheusBytes;
        }
        
#else
        var valueAsString = value.ToString("g", CultureInfo.InvariantCulture);
        var prometheusBytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString);

        var openMetricsBytes = prometheusBytes;

        // Identify whether the original value is floating-point, by checking for presence of the 'e' or '.' characters.
        if (valueAsString.IndexOfAny(DotEChar) == -1)
        {
            // OpenMetrics requires labels containing numeric values to be expressed in floating point format.
            // If all we find is an integer, we add a ".0" to the end to make it a floating point value.
            openMetricsBytes = PrometheusConstants.ExportEncoding.GetBytes(valueAsString + ".0");
        }
#endif

        return new CanonicalLabel(name, prometheusBytes, openMetricsBytes);
    }
}