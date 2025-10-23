using Berberis.Messaging;
using Berberis.Recorder;
using System.Text;

namespace Berberis.SampleApp.Examples.MaxThroughput;

public sealed partial class MaxConsumerService : BackgroundService
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
        //await Task.Delay(3000);

        var destination = "number.inc";

        //using var fs = File.OpenWrite(@"c:\temp\numbers.stream");
        ////using var bs = new BufferedStream(fs, 4 * 1024 * 1024);

        //using var recording = _xBar.Record(destination, fs, new NumberSerialiser(), stoppingToken);

        //var reporter = Task.Run(async () =>
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        var rStats = recording.RecordingStats;

        //        var statsText = $"MPS: {rStats.MessagesPerSecond:N0}; BPS: {rStats.BytesPerSecond:N0}; TB: {rStats.TotalBytes:N0}; SVC: {rStats.AvgServiceTime:N4};";

        //        _logger.LogInformation("{statsText}", statsText);

        //        var stats = recording.UnderlyingSubscription.Statistics.GetStats();
        //        var intervalStats = $"Q: {stats.QueueLength:N0}; Enq: {stats.EnqueueRateInterval:N1} msg/s; Deq: {stats.DequeueRateInterval:N1} msg/s; Pcs: {stats.ProcessRateInterval:N1} msg/s; EnqT: {stats.TotalEnqueuedMessages:N0}; DeqT: {stats.TotalDequeuedMessages:N0}; PcsT: {stats.TotalProcessedMessages:N0}; Lat: {stats.AvgLatencyTimeMsInterval:N4} ms; Svc: {stats.AvgServiceTimeMsInterval:N4} ms";
        //        _logger.LogInformation("{stats}", intervalStats);

        //        await Task.Delay(1000);
        //    }
        //});

        //await recording.MessageLoop;

        using var subscription = _xBar.Subscribe<long>(destination,
            msg => ProcessMessage(msg), token: stoppingToken);

        await subscription.MessageLoop;
    }

    private  ValueTask ProcessMessage(Message<long> message)
    {
        //Thread.SpinWait(4);
        return ValueTask.CompletedTask;

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
