using System;
using System.Threading.Tasks;

namespace Prometheus
{
    public static class CounterExtensions
    {
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
}
