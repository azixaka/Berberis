using Berberis.Messaging;
using Berberis.Portal.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Berberis.Portal.Api.Services;

/// <summary>Background service for streaming CrossBar events to SignalR clients.</summary>
public class EventStreamingService : BackgroundService
{
    private readonly ICrossBar _crossBar;
    private readonly IHubContext<EventsHub> _hubContext;
    private readonly IPortalService _portalService;
    private readonly ILogger<EventStreamingService> _logger;
    private readonly IConfiguration _configuration;

    private ISubscription? _lifecycleSubscription;
    private ISubscription? _tracesSubscription;

    public EventStreamingService(
        ICrossBar crossBar,
        IHubContext<EventsHub> hubContext,
        IPortalService portalService,
        ILogger<EventStreamingService> logger,
        IConfiguration configuration)
    {
        _crossBar = crossBar;
        _hubContext = hubContext;
        _portalService = portalService;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event streaming service starting");

        try
        {
            // Enable lifecycle tracking and message tracing in CrossBar
            _crossBar.LifecycleTrackingEnabled = true;
            _crossBar.MessageTracingEnabled = true;

            // Subscribe to lifecycle events
            _lifecycleSubscription = _crossBar.Subscribe<LifecycleEvent>(
                "$lifecycle",
                async msg => await OnLifecycleEvent(msg.Body),
                "Portal.LifecycleStream",
                stoppingToken);

            // Subscribe to message traces with sampling
            _tracesSubscription = _crossBar.Subscribe<MessageTrace>(
                "$message.traces",
                async msg => await OnMessageTrace(msg.Body),
                "Portal.TracesStream",
                stoppingToken);

            _logger.LogInformation("Event streaming service started successfully");

            // Periodically push metrics to connected clients
            var metricsInterval = _configuration.GetValue<int>("PortalOptions:MetricsUpdateIntervalMs", 5000);
            using var metricsTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(metricsInterval));

            while (!stoppingToken.IsCancellationRequested && await metricsTimer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var overview = await _portalService.GetSystemOverviewAsync();
                    await _hubContext.Clients.Group("metrics").SendAsync("OnMetricsUpdate", overview, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pushing metrics update");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event streaming service");
        }
        finally
        {
            _logger.LogInformation("Event streaming service stopping");
            _lifecycleSubscription?.Dispose();
            _tracesSubscription?.Dispose();
        }
    }

    private async ValueTask OnLifecycleEvent(LifecycleEvent evt)
    {
        try
        {
            _logger.LogDebug("Lifecycle event: {EventType} - {ChannelName}", evt.EventType, evt.ChannelName);
            await _hubContext.Clients.Group("lifecycle").SendAsync("OnLifecycleEvent", evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting lifecycle event");
        }
    }

    private async ValueTask OnMessageTrace(MessageTrace trace)
    {
        try
        {
            // Apply sampling if configured
            var samplingRate = _configuration.GetValue<double>("PortalOptions:MessageTraceSamplingRate", 0.01);
            if (Random.Shared.NextDouble() > samplingRate)
                return;

            await _hubContext.Clients.Group("traces").SendAsync("OnMessageTrace", trace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting message trace");
        }
    }
}
