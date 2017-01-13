using System;

namespace Prometheus.SummaryImpl
{
    class SampleBuffer
    {
        readonly double[] _buffer;

        public SampleBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be > 0");

            _buffer = new double[capacity];
            Position = 0;
        }

        public void Append(double value)
        {
            if (Position >= Capacity)
                throw new InvalidOperationException("Buffer is full");

            _buffer[Position++] = value;
        }

        public double this[int index]
        {
            get
            {
                if (index > Position)
                    throw new ArgumentOutOfRangeException(nameof(index), "Index is greater than position");

                return _buffer[index];
            }
        }

        public void Reset()
        {
            Position = 0;
        }

        public int Position { get; private set; }

        public int Capacity => _buffer.Length;
        public bool IsFull => Position == Capacity;
        public bool IsEmpty => Position == 0;
    }
}
