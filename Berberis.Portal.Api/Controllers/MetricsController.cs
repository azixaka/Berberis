using Berberis.Portal.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Berberis.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(IPortalService portalService, ILogger<MetricsController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>Gets current system metrics.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics()
    {
        try
        {
            var overview = await _portalService.GetSystemOverviewAsync();
            var channels = await _portalService.GetAllChannelsAsync();
            var subscriptions = await _portalService.GetAllSubscriptionsAsync();

            return Ok(new
            {
                overview,
                channels,
                subscriptions,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>Exports metrics in specified format.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMetrics([FromQuery] string format = "json")
    {
        try
        {
            var overview = await _portalService.GetSystemOverviewAsync();
            var channels = await _portalService.GetAllChannelsAsync();
            var subscriptions = await _portalService.GetAllSubscriptionsAsync();

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = GenerateCsv(overview, channels, subscriptions);
                return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"berberis-metrics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
            }

            // Default to JSON
            return Ok(new
            {
                overview,
                channels,
                subscriptions,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting metrics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static string GenerateCsv(
        Contracts.DTOs.SystemOverviewDto overview,
        List<Contracts.DTOs.ChannelInfoDto> channels,
        List<Contracts.DTOs.SubscriptionInfoDto> subscriptions)
    {
        var sb = new StringBuilder();

        // Overview section
        sb.AppendLine("# System Overview");
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Total Channels,{overview.TotalChannels}");
        sb.AppendLine($"Total Subscriptions,{overview.TotalSubscriptions}");
        sb.AppendLine($"Wildcard Subscriptions,{overview.WildcardSubscriptions}");
        sb.AppendLine($"System Throughput (msg/s),{overview.SystemThroughput:F2}");
        sb.AppendLine($"Total Messages Published,{overview.TotalMessagesPublished}");
        sb.AppendLine($"Total Messages Processed,{overview.TotalMessagesProcessed}");
        sb.AppendLine($"Total Timeouts,{overview.TotalTimeouts}");
        sb.AppendLine($"Subscriptions with Backlog,{overview.SubscriptionsWithBacklog}");
        sb.AppendLine();

        // Channels section
        sb.AppendLine("# Channels");
        sb.AppendLine("Name,BodyType,PublishRate,TotalMessages,SubscriptionCount,StoredMessageCount");
        foreach (var channel in channels)
        {
            sb.AppendLine($"{channel.Name},{channel.BodyType},{channel.PublishRate:F2},{channel.TotalMessages},{channel.SubscriptionCount},{channel.StoredMessageCount}");
        }
        sb.AppendLine();

        // Subscriptions section
        sb.AppendLine("# Subscriptions");
        sb.AppendLine("Id,ChannelPattern,Status,QueueDepth,ProcessRate,AvgLatency,P99Latency,TimeoutCount");
        foreach (var sub in subscriptions)
        {
            sb.AppendLine($"{sub.Id},{sub.ChannelPattern},{sub.Status},{sub.QueueDepth},{sub.ProcessRate:F2},{sub.AvgLatencyMs:F2},{sub.PercentileLatencyMs:F2},{sub.TimeoutCount}");
        }

        return sb.ToString();
    }
}
