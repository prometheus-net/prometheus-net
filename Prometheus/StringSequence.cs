using System.Collections;

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
        return new Enumerator(_values, _inheritedValues);
    }

    public ref struct Enumerator
    {
        private int _completedItemsInValues;
        private int _completedInheritedArrays;
        private int _completedItemsInCurrentArray;

        private readonly string[]? _values;
        private readonly string[][]? _inheritedValues;

        public string Current { get; private set; }

        public Enumerator(string[]? values, string[][]? inheritedValues)
        {
            _values = values;
            _inheritedValues = inheritedValues;

            Current = string.Empty;
        }

        public bool MoveNext()
        {
            // Do we have an item to get from the primary values array?
            if (_values != null && _values.Length > _completedItemsInValues)
            {
                Current = _values[_completedItemsInValues];
                _completedItemsInValues++;
                return true;
            }
            // Do we have an item to get from an inherited array?
            else if (_inheritedValues != null && _inheritedValues.Length > _completedInheritedArrays)
            {
                var array = _inheritedValues[_completedInheritedArrays];
                Current = array[_completedItemsInCurrentArray++];

                // Did we complete this array?
                if (array.Length == _completedItemsInCurrentArray)
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
    private StringSequence(StringSequence? inheritFrom, StringSequence? thenFrom, string[]? andFinallyPrepend)
    {
        // Simplify construction if we are given empty inputs.
        if (inheritFrom.HasValue && inheritFrom.Value.Length == 0)
            inheritFrom = null;

        if (thenFrom.HasValue && thenFrom.Value.Length == 0)
            thenFrom = null;

        if (andFinallyPrepend != null && andFinallyPrepend.Length == 0)
            andFinallyPrepend = null;

        // Simplify construction if we have nothing at all.
        if (!inheritFrom.HasValue && !thenFrom.HasValue && andFinallyPrepend == null)
            return;

        // Simplify construction if we just need to match one of the cloneable inputs.
        if (inheritFrom.HasValue && !thenFrom.HasValue && andFinallyPrepend == null)
        {
            this = inheritFrom.Value;
            return;
        }
        else if (thenFrom.HasValue && !inheritFrom.HasValue && andFinallyPrepend == null)
        {
            this = thenFrom.Value;
            return;
        }

        // Anything inherited is already validated.
        if (andFinallyPrepend != null)
        {
            foreach (var ownValue in andFinallyPrepend)
            {
                if (ownValue == null)
                    throw new NotSupportedException("Null values are not supported for metric label names and values.");
            }
        }

        _values = andFinallyPrepend;
        _inheritedValues = InheritFrom(inheritFrom, thenFrom);

        Length = (_values?.Length ?? 0)
            + (inheritFrom.HasValue ? inheritFrom.Value.Length : 0)
            + (thenFrom.HasValue ? thenFrom.Value.Length : 0);

        _hashCode = CalculateHashCode();
    }

    public static StringSequence From(params string[] values)
    {
        return new StringSequence(null, null, values);
    }

    // Creates a new sequence, inheriting all current values and optionally adding more. New values are prepended to the sequence, inherited values come last.
    public StringSequence InheritAndPrepend(params string[] prependValues)
    {
        return new StringSequence(this, null, prependValues);
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

    // Values added by this instance.
    // It may be null because structs have a default ctor and let's be paranoid.
    private readonly string[]? _values;

    // Inherited values from one or more parent instances.
    // It may be null because structs have a default ctor and let's be paranoid.
    private readonly string[][]? _inheritedValues;

    private readonly int _hashCode;

    // We can inherit from one or two parent sequences. Order is "first at the end, second prefixed to it" as is typical (ancestors at the end).
    private static string[][]? InheritFrom(StringSequence? first, StringSequence? second)
    {
        // Expected output: second._values, second._inheritedValues, first._values, first._inheritedValues

        int firstOwnArrayCount = 0;
        int firstInheritedArrayCount = 0;
        int secondOwnArrayCount = 0;
        int secondInheritedArrayCount = 0;

        if (first.HasValue)
        {
            firstOwnArrayCount = first.Value._values?.Length > 0 ? 1 : 0;
            firstInheritedArrayCount = first.Value._inheritedValues?.Length ?? 0;
        }

        if (second.HasValue)
        {
            secondOwnArrayCount = second.Value._values?.Length > 0 ? 1 : 0;
            secondInheritedArrayCount = second.Value._inheritedValues?.Length ?? 0;
        }

        var totalSegmentCount = firstOwnArrayCount + firstInheritedArrayCount + secondOwnArrayCount + secondInheritedArrayCount;

        if (totalSegmentCount == 0)
            return null;

        var result = new string[totalSegmentCount][];

        var targetIndex = 0;

        if (secondOwnArrayCount != 0)
        {
            result[targetIndex++] = second!.Value._values!;
        }

        if (secondInheritedArrayCount != 0)
        {
            Array.Copy(second!.Value._inheritedValues!, 0, result, targetIndex, secondInheritedArrayCount);
            targetIndex += secondInheritedArrayCount;
        }

        if (firstOwnArrayCount != 0)
        {
            result[targetIndex++] = first!.Value._values!;
        }

        if (firstInheritedArrayCount != 0)
        {
            Array.Copy(first!.Value._inheritedValues!, 0, result, targetIndex, firstInheritedArrayCount);
            targetIndex += firstInheritedArrayCount;
        }

        return result;
    }

    private int CalculateHashCode()
    {
        int hashCode = 0;

        var enumerator = GetEnumerator();
        while (enumerator.MoveNext())
        {
            unchecked
            {
                hashCode ^= (enumerator.Current.GetHashCode() * 397);
            }
        }

        return hashCode;
    }

    public bool Contains(string value)
    {
        var enumerator = GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.Equals(value, StringComparison.Ordinal))
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

        var enumerator = GetEnumerator();
        var index = 0;

        while (enumerator.MoveNext())
        {
            result[index++] = enumerator.Current;
        }

        return result;
    }

    public override string ToString()
    {
        // Just for debugging.
        return $"({Length}) {string.Join(", ", ToArray())}";
    }
}
