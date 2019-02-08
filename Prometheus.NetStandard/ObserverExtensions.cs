using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Prometheus
{
    public static class ObserverExtensions
    {
        public static void ObserveDuration(this IObserver observer, Action method)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                method();
            }
            finally
            {
                observer.Observe(stopwatch.Elapsed.TotalSeconds);
            }
        }

        public static T ObserveDuration<T>(this IObserver observer, Func<T> method)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return method();
            }
            finally
            {
                observer.Observe(stopwatch.Elapsed.TotalSeconds);
            }
        }

        public async static Task ObserveDurationAsync(this IObserver observer, Func<Task> method)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await method().ConfigureAwait(false);
            }
            finally
            {
                observer.Observe(stopwatch.Elapsed.TotalSeconds);
            }
        }

        public async static Task<T> ObserveDurationAsync<T>(this IObserver observer, Func<Task<T>> method)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return await method().ConfigureAwait(false);
            }
            finally
            {
                observer.Observe(stopwatch.Elapsed.TotalSeconds);
            }
        }
    }
}
