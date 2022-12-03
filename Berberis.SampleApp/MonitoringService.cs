using Berberis.Messaging;
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
            foreach (var channel in _xBar.GetChannels())
            {
                var chatStats = channel.Statistics.GetStats();

                //_logger.LogInformation("Channel:{channel}, Type:{type}, LastBy: {lastBy}, LastAt: {lastAt}, Rate: {rate:N2}", channel.Name, channel.BodyType.Name, channel.LastPublishedBy, channel.LastPublishedAt.ToUniversalTime().ToString("dd/MM/yyyy HH:mm.fff"), chatStats.PublishRate);
                foreach (var subscription in _xBar.GetChannelSubscriptions(channel.Name))
                {
                    if (!visitedSubs.Contains(subscription.Name))
                    {
                        var stats = subscription.Statistics.GetStats();

                        visitedSubs.Add(subscription.Name);

                        var intervalStats = $"Int: {stats.IntervalMs:N0} ms; Deq: {stats.DequeueRate:N1} msg/s; Pcs: {stats.ProcessRate:N1} msg/s; EnqT: {stats.TotalEnqueuedMessages:N0}; DeqT: {stats.TotalDequeuedMessages:N0}; PcsT: {stats.TotalProcessedMessages:N0};";
                        var longTermStats = $"P90 Lat: {stats.P90LatencyTimeMs:N4}; AvgLat: {stats.AvgLatencyTimeMs:N4}; P90 Svc: {stats.P90ServiceTimeMs:N4}; Avg Svc: {stats.AvgServiceTimeMs:N4}; AvgRsp: {stats.AvgResponseTime:N4} ms; Conf: {stats.ConflationRatio:N4}; QLen: {stats.QueueLength:N0}; Lat/Rsp: {stats.LatencyToResponseTimeRatio:N2}; EAAM: {stats.EstimatedAvgActiveMessages:N4};";

                        _logger.LogInformation("--- Subscription: [{subName}], Rates: {stats}", subscription.Name, intervalStats);
                        _logger.LogInformation("--- Subscription: [{subName}], Times: {stats}", subscription.Name, longTermStats);
                    }

                    //var ms = new MemoryStream();
                    //var writer = new Utf8JsonWriter(ms);
                    //_xBar.MetricsToJson(writer);
                    //writer.Dispose();

                    //var str = Encoding.UTF8.GetString(ms.ToArray());

                    //_logger.LogInformation(str);
                }
            }

            visitedSubs.Clear();
            await Task.Delay(5000);
        }

        await tracingSub?.MessageLoop;
    }
}
