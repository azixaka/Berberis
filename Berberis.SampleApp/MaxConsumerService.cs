using Berberis.Messaging;
using Berberis.Recorder;
using System.Buffers.Binary;
using System.Buffers;
using System.Text;

namespace Berberis.SampleApp;

public sealed class MaxConsumerService : BackgroundService
{
    private readonly ILogger<MaxConsumerService> _logger;
    private readonly ICrossBar _xBar;

    public MaxConsumerService(ILogger<MaxConsumerService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000);

        var destination = "number.inc";

        using var fs = File.OpenWrite(@"c:\temp\numbers.stream");

        using var recording = _xBar.Record(destination, fs, new NumberSerialiser(), stoppingToken);

        var reporter = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var rStats = recording.RecordingStats;

                var statsText = $"MPS: {rStats.MessagesPerSecond:N0}; BPS: {rStats.BytesPerSecond:N0}; TB: {rStats.TotalBytes:N0}; SVC: {rStats.AvgServiceTime:N4};";

                _logger.LogInformation("{statsText}", statsText);

                var stats = recording.UnderlyingSubscription.Statistics.GetStats();
                var intervalStats = $"Int: {stats.IntervalMs:N0} ms; Enq: {stats.EnqueueRateInterval:N1} msg/s; Deq: {stats.DequeueRateInterval:N1} msg/s; Pcs: {stats.ProcessRateInterval:N1} msg/s; EnqT: {stats.TotalEnqueuedMessages:N0}; DeqT: {stats.TotalDequeuedMessages:N0}; PcsT: {stats.TotalProcessedMessages:N0}; AvgLat: {stats.AvgLatencyTimeMsInterval:N4} ms; Avg Svc: {stats.AvgServiceTimeMsInterval:N4} ms";
                _logger.LogInformation("{stats}", intervalStats);

                await Task.Delay(1000);
            }
        });

        await recording.MessageLoop;

        //using var subscription = _xBar.Subscribe<long>(destination,
        //    msg => ProcessMessage(msg), fetchState: true, TimeSpan.FromSeconds(0.5), stoppingToken);

        //await subscription.MessageLoop;
    }

    public sealed class NumberSerialiser : IMessageBodySerializer<long>
    {
        public SerializerVersion Version { get; } = new SerializerVersion(1, 0);

        public long Deserialize(ReadOnlySpan<byte> data)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        public void Serialize(long value, IBufferWriter<byte> writer)
        {
            var span = writer.GetSpan(8);
            BinaryPrimitives.WriteInt64LittleEndian(span, value);
            writer.Advance(8);
        }
    }

    private async ValueTask ProcessMessage(Message<long> message)
    {
        //using (_logger.BeginScope(message.CorrelationId))
        //{
        //    _logger.LogInformation("In process");

        //    using (_logger.BeginScope("new"))
        //    {
        //        await Task.Delay(15);

        //        _logger.LogInformation("Mid");

        //        await Task.Delay(30);

        //        _logger.LogInformation("Finish");
        //    }
        //}
    }
}
