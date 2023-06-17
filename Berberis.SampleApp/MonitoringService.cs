using Berberis.Messaging;
using Berberis.StatsReporters;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        ISubscription tracingSub = null;

        try
        {
            tracingSub = _xBar.Subscribe<MessageTrace>("$message.traces",
                msg =>
                {
                    _logger.LogInformation(msg.Body.ToString());

                    return ValueTask.CompletedTask;
                }, token: stoppingToken);
        }
        catch { }

        var visitedSubs = new HashSet<string>();

        while (!stoppingToken.IsCancellationRequested)
        {
            //var ms = new MemoryStream();
            //var writer = new Utf8JsonWriter(ms);
            //_xBar.MetricsToJson(writer);
            //writer.Dispose();

            //var str = Encoding.UTF8.GetString(ms.ToArray());

            //_logger.LogInformation(str);

            var start = ServiceTimeTracker.GetTicks();

            foreach (var channel in _xBar.GetChannels())
            {
                var chatStats = channel.Statistics.GetStats(true);

                //_logger.LogInformation("Channel:{channel}, Type:{type}, LastBy: {lastBy}, LastAt: {lastAt}, Rate: {rate:F2}", channel.Name, channel.BodyType.Name, channel.LastPublishedBy, channel.LastPublishedAt.ToUniversalTime().ToString("dd/MM/yyyy HH:mm.fff"), chatStats.PublishRate);
                foreach (var subscription in _xBar.GetChannelSubscriptions(channel.Name))
                {
                    if (!visitedSubs.Contains(subscription.Name))
                    {
                        var stats = subscription.Statistics.GetStats(true);

                        visitedSubs.Add(subscription.Name);

                        var intervalStats = $"Int: {stats.IntervalMs:F0} ms; Deq: {stats.DequeueRate:F1} msg/s; Pcs: {stats.ProcessRate:F1} msg/s; EnqT: {stats.TotalEnqueuedMessages:F0}; DeqT: {stats.TotalDequeuedMessages:F0}; PcsT: {stats.TotalProcessedMessages:F0};";
                        var longTermStats = $"P90 Lat: {stats.PercentileLatencyTimeMs:F4}; AvgLat: {stats.AvgLatencyTimeMs:F4}; P90 Svc: {stats.PercentileServiceTimeMs:F4}; Avg Svc: {stats.AvgServiceTimeMs:F4}; AvgRsp: {stats.AvgResponseTime:F4} ms; Conf: {stats.ConflationRatio:F4}; QLen: {stats.QueueLength:F0}; Lat/Rsp: {stats.LatencyToResponseTimeRatio:F2}; EAAM: {stats.EstimatedAvgActiveMessages:F4};";

                        _logger.LogInformation("--- Subscription: [{subName}], Rates: {stats}", subscription.Name, intervalStats);
                        _logger.LogInformation("--- Subscription: [{subName}], Times: {stats}", subscription.Name, longTermStats);
                    }
                }
            }

            visitedSubs.Clear();
            await Task.Delay(5000);
        }

        await tracingSub?.MessageLoop;
    }
}
