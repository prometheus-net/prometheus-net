namespace Prometheus;

public static class CounterExtensions
{
    /// <summary>
    /// Increments the value of the counter to the current UTC time as a Unix timestamp in seconds.
    /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
    /// Operation is ignored if the current value is already greater.
    /// </summary>
    public static void IncToCurrentTimeUtc(this ICounter counter)
    {
        counter.IncTo(LowGranularityTimeSource.GetSecondsFromUnixEpoch());
    }

    /// <summary>
    /// Increments the value of the counter to a specific moment as the UTC Unix timestamp in seconds.
    /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
    /// Operation is ignored if the current value is already greater.
    /// </summary>
    public static void IncToTimeUtc(this ICounter counter, DateTimeOffset timestamp)
    {
        counter.IncTo(TimestampHelpers.ToUnixTimeSecondsAsDouble(timestamp));
    }

    /// <summary>
    /// Executes the provided operation and increments the counter if an exception occurs. The exception is re-thrown.
    /// If an exception filter is specified, only counts exceptions for which the filter returns true.
    /// </summary>
    public static void CountExceptions(this ICounter counter, Action wrapped, Func<Exception, bool>? exceptionFilter = null)
    {
        if (counter == null)
            throw new ArgumentNullException(nameof(counter));

        if (wrapped == null)
            throw new ArgumentNullException(nameof(wrapped));

        try
        {
            wrapped();
        }
        catch (Exception ex) when (exceptionFilter == null || exceptionFilter(ex))
        {
            counter.Inc();
            throw;
        }
    }

    /// <summary>
    /// Executes the provided operation and increments the counter if an exception occurs. The exception is re-thrown.
    /// If an exception filter is specified, only counts exceptions for which the filter returns true.
    /// </summary>
    public static TResult CountExceptions<TResult>(this ICounter counter, Func<TResult> wrapped, Func<Exception, bool>? exceptionFilter = null)
    {
        if (counter == null)
            throw new ArgumentNullException(nameof(counter));

        if (wrapped == null)
            throw new ArgumentNullException(nameof(wrapped));

        try
        {
            return wrapped();
        }
        catch (Exception ex) when (exceptionFilter == null || exceptionFilter(ex))
        {
            counter.Inc();
            throw;
        }
    }

    /// <summary>
    /// Executes the provided async operation and increments the counter if an exception occurs. The exception is re-thrown.
    /// If an exception filter is specified, only counts exceptions for which the filter returns true.
    /// </summary>
    public static async Task CountExceptionsAsync(this ICounter counter, Func<Task> wrapped, Func<Exception, bool>? exceptionFilter = null)
    {
        if (counter == null)
            throw new ArgumentNullException(nameof(counter));

        if (wrapped == null)
            throw new ArgumentNullException(nameof(wrapped));

        try
        {
            await wrapped().ConfigureAwait(false);
        }
        catch (Exception ex) when (exceptionFilter == null || exceptionFilter(ex))
        {
            counter.Inc();
            throw;
        }
    }

    /// <summary>
    /// Executes the provided async operation and increments the counter if an exception occurs. The exception is re-thrown.
    /// If an exception filter is specified, only counts exceptions for which the filter returns true.
    /// </summary>
    public static async Task<TResult> CountExceptionsAsync<TResult>(this ICounter counter, Func<Task<TResult>> wrapped, Func<Exception, bool>? exceptionFilter = null)
    {
        if (counter == null)
            throw new ArgumentNullException(nameof(counter));

        if (wrapped == null)
            throw new ArgumentNullException(nameof(wrapped));

        try
        {
            return await wrapped().ConfigureAwait(false);
        }
        catch (Exception ex) when (exceptionFilter == null || exceptionFilter(ex))
        {
            counter.Inc();
            throw;
        }
    }
}
