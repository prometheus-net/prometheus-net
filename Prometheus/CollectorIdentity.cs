namespace Prometheus;

/// <summary>
/// Uniquely identifies a specific collector within a family. Different collectors are used for different label combinations.
/// * Any difference in static labels (keys or values) means it is a different collector.
/// * Any difference in the names of instance labels means it is a different collector.
/// </summary>
internal readonly struct CollectorIdentity(StringSequence instanceLabelNames, LabelSequence staticLabels) : IEquatable<CollectorIdentity>
{
    public readonly StringSequence InstanceLabelNames = instanceLabelNames;
    public readonly LabelSequence StaticLabels = staticLabels;

    private readonly int _hashCode = CalculateHashCode(instanceLabelNames, staticLabels);

    public bool Equals(CollectorIdentity other)
    {
        if (_hashCode != other._hashCode)
            return false;

        if (InstanceLabelNames.Length != other.InstanceLabelNames.Length)
            return false;

        if (!InstanceLabelNames.Equals(other.InstanceLabelNames))
            return false;

        if (!StaticLabels.Equals(other.StaticLabels))
            return false;

        return true;
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    private static int CalculateHashCode(StringSequence instanceLabelNames, LabelSequence staticLabels)
    {
        unchecked
        {
            int hashCode = 0;

            hashCode ^= instanceLabelNames.GetHashCode() * 397;
            hashCode ^= staticLabels.GetHashCode() * 397;

            return hashCode;
        }
    }

    public override string ToString()
    {
        return $"{_hashCode}{{{InstanceLabelNames.Length} + {StaticLabels.Length}}}";
    }

    public override bool Equals(object? obj)
    {
        return obj is CollectorIdentity identity && Equals(identity);
    }
}