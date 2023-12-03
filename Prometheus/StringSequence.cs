using System.Runtime.CompilerServices;

namespace Prometheus;

/// <summary>
/// Used for maintaining low-allocation-overhead ordered lists of strings, such as those used for metric label names and label values.
/// The list can be constructed from multiple segments that come from different types of data sources, unified into a single sequence by this type.
/// </summary>
/// <remarks>
/// We assume (as an optimization) that the segments the sequence is made from never change.
/// We compare values using ordinal comparison.
/// 
/// We explicitly do not mark this sequence as enumerable or a collection type, to prevent accidentally using a non-performance-tuned enumeration method.
/// </remarks>
internal readonly struct StringSequence : IEquatable<StringSequence>
{
    public static readonly StringSequence Empty = new();

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_values.Span, _inheritedValues ?? []);
    }

    public ref struct Enumerator
    {
        private int _completedItemsInValues;
        private int _completedInheritedArrays;
        private int _completedItemsInCurrentArray;

        private readonly ReadOnlySpan<string> _values;
        private readonly ReadOnlyMemory<string>[] _inheritedValues;

        private ReadOnlySpan<string> _currentArray;
        private string _current;

        public readonly string Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator(ReadOnlySpan<string> values, ReadOnlyMemory<string>[] inheritedValues)
        {
            _values = values;
            _inheritedValues = inheritedValues;

            _current = string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // Do we have an item to get from the primary values array?
            if (_values.Length > _completedItemsInValues)
            {
                _current = _values[_completedItemsInValues];
                _completedItemsInValues++;
                return true;
            }
            // Do we have an item to get from an inherited array?
            else if (_inheritedValues.Length > _completedInheritedArrays)
            {
                if (_completedItemsInCurrentArray == 0)
                    _currentArray = _inheritedValues[_completedInheritedArrays].Span;

                _current = _currentArray[_completedItemsInCurrentArray++];

                // Did we complete this array?
                if (_currentArray.Length == _completedItemsInCurrentArray)
                {
                    _completedItemsInCurrentArray = 0;
                    _completedInheritedArrays++;
                }

                return true;
            }
            else
            {
                // All done!
                return false;
            }
        }
    }

    public int Length { get; }

    public bool IsEmpty => Length == 0;

    public bool Equals(StringSequence other)
    {
        if (_hashCode != other._hashCode) return false;
        if (Length != other.Length) return false;

        var left = GetEnumerator();
        var right = other.GetEnumerator();

        for (var i = 0; i < Length; i++)
        {
            if (!left.MoveNext()) throw new Exception("API contract violation.");
            if (!right.MoveNext()) throw new Exception("API contract violation.");

            if (!string.Equals(left.Current, right.Current, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        if (obj is StringSequence ss)
            return Equals(ss);

        return false;
    }

    public override int GetHashCode() => _hashCode;

    // There are various ways we can make a StringSequence, comining one or two parents and maybe adding some extra to the start.
    // This ctor tries to account for all these options.
    private StringSequence(StringSequence inheritFrom, StringSequence thenFrom, ReadOnlyMemory<string> andFinallyPrepend)
    {
        // Simplify construction if we just need to match one of the cloneable inputs.
        if (!inheritFrom.IsEmpty && thenFrom.IsEmpty && andFinallyPrepend.Length == 0)
        {
            this = inheritFrom;
            return;
        }
        else if (!thenFrom.IsEmpty && inheritFrom.IsEmpty && andFinallyPrepend.Length == 0)
        {
            this = thenFrom;
            return;
        }

        // Simplify construction if we have nothing at all.
        if (inheritFrom.IsEmpty && thenFrom.IsEmpty && andFinallyPrepend.Length == 0)
            return;

        // Anything inherited is already validated. Perform a sanity check on anything new.
        if (andFinallyPrepend.Length != 0)
        {
            var span = andFinallyPrepend.Span;

            for (var i = 0; i < span.Length; i++)
            {
                var ownValue = span[i];

                if (ownValue == null)
                    throw new NotSupportedException("Null values are not supported for metric label names and values.");
            }
        }

        _values = andFinallyPrepend;
        _inheritedValues = InheritFrom(inheritFrom, thenFrom);

        Length = _values.Length + inheritFrom.Length + thenFrom.Length;

        _hashCode = CalculateHashCode();
    }

    public static StringSequence From(params string[] values)
    {
        if (values.Length == 0)
            return Empty;

        return new StringSequence(Empty, Empty, values);
    }

    public static StringSequence From(ReadOnlyMemory<string> values)
    {
        if (values.Length == 0)
            return Empty;

        return new StringSequence(Empty, Empty, values);
    }

    // Creates a new sequence, inheriting all current values and optionally adding more. New values are prepended to the sequence, inherited values come last.
    public StringSequence InheritAndPrepend(params string[] prependValues)
    {
        return new StringSequence(this, Empty, prependValues);
    }

    // Creates a new sequence, inheriting all current values and optionally adding more. New values are prepended to the sequence, inherited values come last.
    public StringSequence InheritAndPrepend(StringSequence prependValues)
    {
        return new StringSequence(this, prependValues, null);
    }

    // Creates a new sequence, concatenating another string sequence (by inheriting from it).
    public StringSequence Concat(StringSequence concatenatedValues)
    {
        return new StringSequence(concatenatedValues, this, null);
    }

    // Values added by this instance. It may be empty.
    private readonly ReadOnlyMemory<string> _values;

    // Inherited values from one or more parent instances.
    // It may be null because structs have a default ctor that zero-initializes them, so watch out.
    private readonly ReadOnlyMemory<string>[]? _inheritedValues;

    private readonly int _hashCode;

    // We can inherit from one or two parent sequences. Order is "first at the end, second prefixed to it" as is typical (ancestors at the end).
    private static ReadOnlyMemory<string>[]? InheritFrom(StringSequence first, StringSequence second)
    {
        // Expected output: second._values, second._inheritedValues, first._values, first._inheritedValues

        int firstOwnArrayCount = 0;
        int firstInheritedArrayCount = 0;
        int secondOwnArrayCount = 0;
        int secondInheritedArrayCount = 0;

        if (!first.IsEmpty)
        {
            firstOwnArrayCount = first._values.Length > 0 ? 1 : 0;
            firstInheritedArrayCount = first._inheritedValues?.Length ?? 0;
        }

        if (!second.IsEmpty)
        {
            secondOwnArrayCount = second._values.Length > 0 ? 1 : 0;
            secondInheritedArrayCount = second._inheritedValues?.Length ?? 0;
        }

        var totalSegmentCount = firstOwnArrayCount + firstInheritedArrayCount + secondOwnArrayCount + secondInheritedArrayCount;

        if (totalSegmentCount == 0)
            return null;

        var result = new ReadOnlyMemory<string>[totalSegmentCount];

        var targetIndex = 0;

        if (secondOwnArrayCount != 0)
        {
            result[targetIndex++] = second._values;
        }

        if (secondInheritedArrayCount != 0)
        {
            Array.Copy(second._inheritedValues!, 0, result, targetIndex, secondInheritedArrayCount);
            targetIndex += secondInheritedArrayCount;
        }

        if (firstOwnArrayCount != 0)
        {
            result[targetIndex++] = first._values;
        }

        if (firstInheritedArrayCount != 0)
        {
            Array.Copy(first._inheritedValues!, 0, result, targetIndex, firstInheritedArrayCount);
            targetIndex += firstInheritedArrayCount;
        }

        return result;
    }

    private int CalculateHashCode()
    {
        int hashCode = 0;

        foreach (var item in this)
        {
            unchecked
            {
                hashCode ^= (item.GetHashCode() * 397);
            }
        }

        return hashCode;
    }

    public bool Contains(string value)
    {
        foreach (var item in this)
        {
            if (item.Equals(value, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Copies the sequence to a new array. Try keep this out of any hot path at runtime, please!
    /// </summary>
    public string[] ToArray()
    {
        var result = new string[Length];

        var index = 0;

        foreach (var item in this)
            result[index++] = item;

        return result;
    }

    public override string ToString()
    {
        // Just for debugging.
        return $"({Length}) {string.Join(", ", ToArray())}";
    }
}
