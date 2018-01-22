using System;
using System.Globalization;
using System.Threading;

namespace Prometheus.Advanced
{
    public struct ThreadSafeDouble
    {
        private long _value;

        public ThreadSafeDouble(double value)
        {
            _value = BitConverter.DoubleToInt64Bits(value);
        }

        public double Value
        {
            get
            {
                return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _value));
            }
            set
            {
                Interlocked.Exchange(ref _value, BitConverter.DoubleToInt64Bits(value));
            }
        }

        public void Add(double increment)
        {
            while (true)
            {
                long initialValue = _value;
                double computedValue = BitConverter.Int64BitsToDouble(initialValue) + increment;

                //Compare exchange will only set the computed value if it is equal to the expected value
                //It will always return the the value of _value prior to the exchange (whether it happens or not)
                //So, only exit the loop if the value was what we expected it to be (initialValue) at the time of exchange otherwise another thread updated and we need to try again.
                if (initialValue == Interlocked.CompareExchange(ref _value, BitConverter.DoubleToInt64Bits(computedValue), initialValue))
                    return;
            }
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            if (obj is ThreadSafeDouble)
                return Value.Equals(((ThreadSafeDouble)obj).Value);

            return Value.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
