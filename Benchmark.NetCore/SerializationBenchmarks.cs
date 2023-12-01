using System.IO.Pipes;
using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
//[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.CpuSampling)]
public class SerializationBenchmarks
{
    public enum OutputStreamType
    {
        /// <summary>
        /// A null stream that just does nothing and immediately returns.
        /// Low overhead but also unrealistic in terms of asynchronous I/O behavior.
        /// </summary>
        Null,

        /// <summary>
        /// A stream that does nothing except yielding the task/thread to take up nonzero time.
        /// Tries to increase the overhead from async/await task management that might occur.
        /// </summary>
        Yield,

        /// <summary>
        /// A named pipe connection. Something halfway between null and a real network connection.
        /// Named pipes appear to be super slow when dealing with small amounts of data, so optimizing
        /// this scenario is valuable to ensure that we perform well with real network connections that
        /// may have similar limitations (depending on OS and network stack).
        /// </summary>
        NamedPipe
    }

    [Params(OutputStreamType.Null, OutputStreamType.NamedPipe, OutputStreamType.Yield)]
    public OutputStreamType StreamType { get; set; }

    // Metric -> Variant -> Label values
    private static readonly string[][][] _labelValueRows;

    private const int _metricCount = 100;
    private const int _variantCount = 100;
    private const int _labelCount = 5;

    private const string _help = "arbitrary help message for metric lorem ipsum dolor golor bolem";

    static SerializationBenchmarks()
    {
        _labelValueRows = new string[_metricCount][][];

        for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
        {
            var variants = new string[_variantCount][];
            _labelValueRows[metricIndex] = variants;

            for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
            {
                var values = new string[_labelCount];
                _labelValueRows[metricIndex][variantIndex] = values;

                for (var labelIndex = 0; labelIndex < _labelCount; labelIndex++)
                    values[labelIndex] = $"metric{metricIndex:D2}_label{labelIndex:D2}_variant{variantIndex:D2}";
            }
        }
    }

    private readonly CollectorRegistry _registry = Metrics.NewCustomRegistry();
    private readonly Counter[] _counters;
    private readonly Gauge[] _gauges;
    private readonly Summary[] _summaries;
    private readonly Histogram[] _histograms;

    public SerializationBenchmarks()
    {
        _counters = new Counter[_metricCount];
        _gauges = new Gauge[_metricCount];
        _summaries = new Summary[_metricCount];
        _histograms = new Histogram[_metricCount];

        var factory = Metrics.WithCustomRegistry(_registry);

        // Just use 1st variant for the keys (all we care about are that there is some name-like value in there).
        for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
        {
            _counters[metricIndex] = factory.CreateCounter($"counter{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
            _gauges[metricIndex] = factory.CreateGauge($"gauge{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
            _summaries[metricIndex] = factory.CreateSummary($"summary{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
            _histograms[metricIndex] = factory.CreateHistogram($"histogram{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
        }

        // Genmerate some sample data so the metrics are not all zero-initialized.
        var exemplarLabelPair = Exemplar.Key("traceID").WithValue("bar");
        for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
            for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
            {
                _counters[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Inc(Exemplar.From(exemplarLabelPair));
                _gauges[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Inc();
                _summaries[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Observe(variantIndex);
                _histograms[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Observe(variantIndex, Exemplar.From(exemplarLabelPair));
            }
    }

    [GlobalSetup]
    public void Setup()
    {
        if (StreamType == OutputStreamType.Null)
        {
            _outputStream = Stream.Null;
        }
        else if (StreamType == OutputStreamType.Yield)
        {
            _outputStream = YieldStream.Default;
        }
        else if (StreamType == OutputStreamType.NamedPipe)
        {
            var pipeName = StartStreamReader();

            var pipeStream = new NamedPipeClientStream(".", pipeName.ToString(), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            pipeStream.Connect(TimeSpan.FromSeconds(1));

            _outputStream = pipeStream;
        }
        else
            throw new NotSupportedException();
    }

    // Will be reused by all the iterations - the serializer does not take ownership nor close the stream.
    private Stream _outputStream;

    // When this is cancelled, the output stream reader will stop listening for new connections and reading data from existing ones.
    private readonly CancellationTokenSource _outputStreamReaderCts = new();

    // We just read data into it, we do not care about the contents.
    // While we do not expect concurrent access, it is fine if it does happen because this data is never consumed
    private static readonly byte[] _readBuffer = new byte[1024];

    /// <summary>
    /// Starts listening on a random port on the loopback interface and returns the name of the created pipe stream.
    /// </summary>
    private Guid StartStreamReader()
    {
        var name = Guid.NewGuid();
        var server = new NamedPipeServerStream(name.ToString(), PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var cancel = _outputStreamReaderCts.Token;

        _ = Task.Run(async delegate
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    await server.WaitForConnectionAsync(cancel);
                    Console.WriteLine("Received a connection.");

                    try
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            var bytesRead = await server.ReadAsync(_readBuffer, cancel);

                            if (bytesRead == 0)
                                break;
                        }
                    }
                    finally
                    {
                        server.Disconnect();
                    }
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                // Expected
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected exception in output stream reader: {ex}");
            }
            finally
            {
                server.Dispose();
            }
        });

        return name;
    }

    [Benchmark]
    public async Task CollectAndSerialize()
    {
        await _registry.CollectAndSerializeAsync(new TextSerializer(_outputStream), default);
    }

    [Benchmark]
    public async Task CollectAndSerializeOpenMetrics()
    {
        await _registry.CollectAndSerializeAsync(new TextSerializer(_outputStream, ExpositionFormat.OpenMetricsText), default);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _outputStreamReaderCts.Cancel();
        _outputStreamReaderCts.Dispose();
    }
}
