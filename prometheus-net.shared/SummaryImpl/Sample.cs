namespace Prometheus
{
    // Sample holds an observed value and meta information for compression. 
    struct Sample
    {
        public double Value;
        public double Width;
        public double Delta;
    }
}