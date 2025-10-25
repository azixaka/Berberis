using Microsoft.AspNetCore.SignalR;

namespace Berberis.Portal.Api.Hubs;

/// <summary>SignalR hub for streaming real-time events to clients.</summary>
public class EventsHub : Hub
{
    private readonly ILogger<EventsHub> _logger;

    public EventsHub(ILogger<EventsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client requests to subscribe to lifecycle events.</summary>
    public async Task SubscribeToLifecycle()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "lifecycle");
        _logger.LogDebug("Client {ConnectionId} subscribed to lifecycle events", Context.ConnectionId);
    }

    /// <summary>Client requests to unsubscribe from lifecycle events.</summary>
    public async Task UnsubscribeFromLifecycle()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lifecycle");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from lifecycle events", Context.ConnectionId);
    }

    /// <summary>Client requests to subscribe to message traces with sampling rate.</summary>
    public async Task SubscribeToTraces(double samplingRate = 0.01)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "traces");
        _logger.LogDebug("Client {ConnectionId} subscribed to message traces with sampling rate {SamplingRate}",
            Context.ConnectionId, samplingRate);
    }

    /// <summary>Client requests to unsubscribe from message traces.</summary>
    public async Task UnsubscribeFromTraces()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "traces");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from message traces", Context.ConnectionId);
    }

    /// <summary>Client requests to subscribe to metrics updates.</summary>
    public async Task SubscribeToMetrics(int intervalMs = 5000)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "metrics");
        _logger.LogDebug("Client {ConnectionId} subscribed to metrics with interval {IntervalMs}ms",
            Context.ConnectionId, intervalMs);
    }

    /// <summary>Client requests to unsubscribe from metrics updates.</summary>
    public async Task UnsubscribeFromMetrics()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "metrics");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from metrics", Context.ConnectionId);
    }
}
