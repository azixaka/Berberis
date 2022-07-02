using Berberis.Messaging;

namespace Berberis.SampleApp;

public sealed class MonitoringService : BackgroundService
{
    private readonly ILogger<MonitoringService> _logger;
    private readonly ICrossBar _xBar;

    public MonitoringService(ILogger<MonitoringService> logger, ICrossBar xBar)
    {
        _logger = logger;
        _xBar = xBar;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        //_xBar.TracingEnabled = true;

        using var tracingSub = _xBar.Subscribe<MessageTrace>("$message.traces",
            msg =>
            {
                _logger.LogInformation(msg.Body.ToString());

                return ValueTask.CompletedTask;
            });

        var tracingLoop = tracingSub.RunReadLoopAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var channel in _xBar.GetChannels())
            {
                _logger.LogInformation("Channel:{channel}, Type:{type}, LastBy: {lastBy}, LastAt: {lastAt}", channel.Name, channel.BodyType.Name, channel.LastPublishedBy, channel.LastPublishedAt.ToUniversalTime());

                foreach (var subscription in _xBar.GetChannelSubscriptions(channel.Name))
                {
                    var stats = subscription.Statistics.GetStats();

                    var intervalStats = $"Int: {stats.IntervalMs:N0} ms; Enqueued: {stats.MessagesPerSecondEnqueue:N1} msg/s; Dequeued: {stats.MessagesPerSecondDequeued:N1} msg/s; Processed: {stats.MessagesPerSecondProcessed:N1} msg/s; Total Enqueued: {stats.TotalEnqueuedMessages:N0}; Total Dequeued: {stats.TotalDequeuedMessages:N0}; Total Processed: {stats.TotalProcessedMessages:N0}; Avg Latency: {stats.AvgLatencyTimeMs:N4} ms; Avg Svc: {stats.AvgServiceTimeMs:N4} ms";
                    var longTermStats = $"Conf: {stats.ConflationRate:N4}; QLen: {stats.QueueLength:N0}; Lat-RspRatio: {stats.LatencyToResponseTimeRatio:N2}; DequeueRate: {stats.DequeueRate:N1} msg/s; EstAvgActiveMsgsDeq: {stats.EstimatedAvgActiveMessagesDequeue:N4}; ProcessRate: {stats.ProcessRate:N2} msg/s; EstAvgActiveMsgsProcess: {stats.EstimatedAvgActiveMessagesProcess:N4}; Avg Latency: {stats.AvgAllLatencyTimeMs:N4} ms; Avg Svc: {stats.AvgAllServiceTimeMs:N4} ms";

                    _logger.LogInformation("--- Subscription: [{subName}], Interval Stats: {stats}", subscription.Name, intervalStats);
                    _logger.LogInformation("--- Subscription: [{subName}], Long-term Stats: {stats}", subscription.Name, longTermStats);
                }
            }

            await Task.Delay(1000);
        }

        await tracingLoop;
    }
}
