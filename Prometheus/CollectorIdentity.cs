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
        public readonly string[] LabelNames;

        public CollectorIdentity(string name, string[] labelNames)
        {
            Name = name;
            LabelNames = labelNames;
        }

        public bool Equals(CollectorIdentity other)
        {
            if (!string.Equals(Name, other.Name, StringComparison.InvariantCulture))
                return false;

            if (LabelNames.Length != other.LabelNames.Length)
                return false;

            for (var i = 0; i < LabelNames.Length; i++)
                if (!string.Equals(LabelNames[i], other.LabelNames[i], StringComparison.InvariantCulture))
                    return false;

            return true;
        }
    }
}
