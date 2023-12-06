// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Prometheus;

// Copy-pasted from https://github.com/dotnet/efcore/blob/main/src/Shared/NonCapturingLazyInitializer.cs
// Crudely modified to inline dependencies and reduce functionality down to .NET Fx compatible level.
internal static class NonCapturingLazyInitializer
{
    public static TValue EnsureInitialized<TParam, TValue>(
        ref TValue? target,
        TParam param,
        Func<TParam, TValue> valueFactory)
        where TValue : class
    {
        var tmp = Volatile.Read(ref target);
        if (tmp != null)
        {
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            return tmp;
        }

        Interlocked.CompareExchange(ref target, valueFactory(param), null);

        return target;
    }

    public static TValue EnsureInitialized<TParam1, TParam2, TValue>(
        ref TValue? target,
        TParam1 param1,
        TParam2 param2,
        Func<TParam1, TParam2, TValue> valueFactory)
        where TValue : class
    {
        var tmp = Volatile.Read(ref target);
        if (tmp != null)
        {
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            return tmp;
        }

        Interlocked.CompareExchange(ref target, valueFactory(param1, param2), null);

        return target;
    }

    public static TValue EnsureInitialized<TParam1, TParam2, TParam3, TValue>(
        ref TValue? target,
        TParam1 param1,
        TParam2 param2,
        TParam3 param3,
        Func<TParam1, TParam2, TParam3, TValue> valueFactory)
        where TValue : class
    {
        var tmp = Volatile.Read(ref target);
        if (tmp != null)
        {
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            return tmp;
        }

        Interlocked.CompareExchange(ref target, valueFactory(param1, param2, param3), null);

        return target;
    }

    public static TValue EnsureInitialized<TParam, TValue>(
        ref TValue target,
        ref bool initialized,
        TParam param,
        Func<TParam, TValue> valueFactory)
        where TValue : class?
    {
        var alreadyInitialized = Volatile.Read(ref initialized);
        if (alreadyInitialized)
        {
            var value = Volatile.Read(ref target);
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            DebugAssert(value != null, $"value was null in {nameof(EnsureInitialized)} after check");
            return value;
        }

        Volatile.Write(ref target, valueFactory(param));
        Volatile.Write(ref initialized, true);

        return target;
    }

    public static TValue EnsureInitialized<TValue>(
        ref TValue? target,
        TValue value)
        where TValue : class
    {
        var tmp = Volatile.Read(ref target);
        if (tmp != null)
        {
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            return tmp;
        }

        Interlocked.CompareExchange(ref target, value, null);

        return target;
    }

    public static TValue EnsureInitialized<TParam, TValue>(
        ref TValue? target,
        TParam param,
        Action<TParam> valueFactory)
        where TValue : class
    {
        var tmp = Volatile.Read(ref target);
        if (tmp != null)
        {
            DebugAssert(target != null, $"target was null in {nameof(EnsureInitialized)} after check");
            return tmp;
        }

        valueFactory(param);

        var tmp2 = Volatile.Read(ref target);
        DebugAssert(
            target != null && tmp2 != null,
            $"{nameof(valueFactory)} did not initialize {nameof(target)} in {nameof(EnsureInitialized)}");
#pragma warning disable CS8603 // Possible null reference return.
        return tmp2;
#pragma warning restore CS8603 // Possible null reference return.
    }

    [Conditional("DEBUG")]
    private static void DebugAssert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Check.DebugAssert failed: {message}");
        }
    }
}
