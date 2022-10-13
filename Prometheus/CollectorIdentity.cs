namespace Prometheus
{
    /// <summary>
    /// Represents the values that make up a single collector's unique identity, used during collector registration.
    /// If these values match an existing collector, we will reuse it (or throw if metadata mismatches).
    /// If these values do not match any existing collector, we will create a new collector.
    /// </summary>
    internal struct CollectorIdentity : IEquatable<CollectorIdentity>
    {
        public readonly string Name;
        public readonly StringSequence InstanceLabelNames;
        public readonly StringSequence StaticLabelNames;
        
        private readonly int _hashCode;

        public CollectorIdentity(string name, StringSequence instanceLabelNames, StringSequence staticLabelNames)
        {
            Name = name;
            InstanceLabelNames = instanceLabelNames;
            StaticLabelNames = staticLabelNames;

            _hashCode = CalculateHashCode(name, instanceLabelNames, staticLabelNames);
        }

        public bool Equals(CollectorIdentity other)
        {
            if (!string.Equals(Name, other.Name, StringComparison.Ordinal))
                return false;

            if (_hashCode != other._hashCode)
                return false;

            if (InstanceLabelNames.Length != other.InstanceLabelNames.Length)
                return false;

            if (!InstanceLabelNames.Equals(other.InstanceLabelNames))
                return false;

            if (!StaticLabelNames.Equals(other.StaticLabelNames))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private static int CalculateHashCode(string name, StringSequence instanceLabelNames, StringSequence staticLabelNames)
        {
            unchecked
            {
                int hashCode = 0;

                hashCode ^= name.GetHashCode() * 31;
                hashCode ^= instanceLabelNames.GetHashCode() * 397;
                hashCode ^= staticLabelNames.GetHashCode() * 397;

                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{Name}{{{InstanceLabelNames.Length + StaticLabelNames.Length}}}";
        }
    }
}
