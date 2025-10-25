using Berberis.Messaging;
using Berberis.Portal.Contracts.DTOs;

namespace Berberis.Portal.Api.Services;

/// <summary>Service for accessing CrossBar system information.</summary>
public class PortalService : IPortalService
{
    private readonly ICrossBar _crossBar;
    private readonly ILogger<PortalService> _logger;
    private readonly CrossBarOptions _options;

    public PortalService(ICrossBar crossBar, ILogger<PortalService> logger, CrossBarOptions options)
    {
        _crossBar = crossBar;
        _logger = logger;
        _options = options;
    }

    public Task<SystemOverviewDto> GetSystemOverviewAsync()
    {
        try
        {
            var channels = _crossBar.GetChannels();
            var overview = new SystemOverviewDto
            {
                TotalChannels = channels.Count,
                SystemThroughput = channels.Sum(c => c.Statistics.GetStats(false).PublishRate),
                TotalMessagesPublished = channels.Sum(c => c.Statistics.GetStats(false).TotalMessages)
            };

            var allSubscriptions = new List<CrossBar.SubscriptionInfo>();
            foreach (var channel in channels)
            {
                var subs = _crossBar.GetChannelSubscriptions(channel.Name);
                if (subs != null)
                    allSubscriptions.AddRange(subs);
            }

            overview.TotalSubscriptions = allSubscriptions.Count;
            overview.WildcardSubscriptions = allSubscriptions.Count(s => s.IsWildcard);
            overview.TotalMessagesProcessed = allSubscriptions.Sum(s => s.Statistics.GetStats(false).TotalProcessedMessages);
            overview.TotalTimeouts = allSubscriptions.Sum(s => s.Statistics.GetStats(false).NumOfTimeouts);
            overview.SubscriptionsWithBacklog = allSubscriptions.Count(s => s.Statistics.GetStats(false).QueueLength > 0);

            return Task.FromResult(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system overview");
            throw;
        }
    }

    public Task<List<ChannelInfoDto>> GetAllChannelsAsync(string? searchTerm = null)
    {
        try
        {
            var channels = _crossBar.GetChannels();
            var result = channels.Select(c =>
            {
                var stats = c.Statistics.GetStats(false);
                var subs = _crossBar.GetChannelSubscriptions(c.Name);
                return new ChannelInfoDto
                {
                    Name = c.Name,
                    BodyType = c.BodyType.Name,
                    PublishRate = stats.PublishRate,
                    TotalMessages = stats.TotalMessages,
                    SubscriptionCount = subs?.Count ?? 0,
                    StoredMessageCount = c.StoredMessageCount,
                    LastPublishedAt = c.LastPublishedAt == default ? null : c.LastPublishedAt,
                    LastPublishedBy = c.LastPublishedBy
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                result = result.Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all channels");
            throw;
        }
    }

    public Task<ChannelDetailDto?> GetChannelDetailsAsync(string channelName)
    {
        try
        {
            var channels = _crossBar.GetChannels();
            var channel = channels.FirstOrDefault(c => c.Name == channelName);
            if (channel.Name == null)
                return Task.FromResult<ChannelDetailDto?>(null);

            var stats = channel.Statistics.GetStats(false);
            var subs = _crossBar.GetChannelSubscriptions(channelName);
            var result = new ChannelDetailDto
            {
                Name = channel.Name,
                BodyType = channel.BodyType.Name,
                PublishRate = stats.PublishRate,
                TotalMessages = stats.TotalMessages,
                SubscriptionCount = subs?.Count ?? 0,
                StoredMessageCount = channel.StoredMessageCount,
                LastPublishedAt = channel.LastPublishedAt == default ? null : channel.LastPublishedAt,
                LastPublishedBy = channel.LastPublishedBy,
                Subscriptions = subs?.Select(s => MapToSubscriptionInfoDto(s)).ToList() ?? new()
            };

            return Task.FromResult<ChannelDetailDto?>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel details for {ChannelName}", channelName);
            throw;
        }
    }

    public Task<List<SubscriptionInfoDto>> GetAllSubscriptionsAsync(string? searchTerm = null, string? statusFilter = null)
    {
        try
        {
            var channels = _crossBar.GetChannels();
            var allSubscriptions = new List<SubscriptionInfoDto>();

            foreach (var channel in channels)
            {
                var subs = _crossBar.GetChannelSubscriptions(channel.Name);
                if (subs != null)
                {
                    allSubscriptions.AddRange(subs.Select(MapToSubscriptionInfoDto));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                allSubscriptions = allSubscriptions.Where(s =>
                    s.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    s.ChannelPattern.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                allSubscriptions = allSubscriptions.Where(s =>
                    s.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return Task.FromResult(allSubscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all subscriptions");
            throw;
        }
    }

    public Task<SubscriptionDetailDto?> GetSubscriptionDetailsAsync(string subscriptionId)
    {
        try
        {
            var channels = _crossBar.GetChannels();
            foreach (var channel in channels)
            {
                var subs = _crossBar.GetChannelSubscriptions(channel.Name);
                var sub = subs?.FirstOrDefault(s => s.Name == subscriptionId);
                if (sub.HasValue && sub.Value.Name != null)
                {
                    return Task.FromResult<SubscriptionDetailDto?>(MapToSubscriptionDetailDto(sub.Value));
                }
            }

            return Task.FromResult<SubscriptionDetailDto?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription details for {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public Task<bool> SuspendSubscriptionAsync(string subscriptionId)
    {
        // TODO: This requires access to ISubscription.IsProcessingSuspended
        // Need to add CrossBar API: ISubscription? GetSubscription(string name)
        _logger.LogWarning("Suspend subscription not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(false);
    }

    public Task<bool> ResumeSubscriptionAsync(string subscriptionId)
    {
        // TODO: This requires access to ISubscription.IsProcessingSuspended
        // Need to add CrossBar API: ISubscription? GetSubscription(string name)
        _logger.LogWarning("Resume subscription not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(false);
    }

    public Task<bool> DetachSubscriptionAsync(string subscriptionId)
    {
        // TODO: This requires access to ISubscription.IsDetached or Dispose()
        // Need to add CrossBar API: ISubscription? GetSubscription(string name)
        _logger.LogWarning("Detach subscription not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(false);
    }

    public Task<List<string>> GetChannelStateKeysAsync(string channelName)
    {
        // TODO: This requires a CrossBar API to enumerate state keys
        // Current API only has GetChannelState<TBody>() which requires knowing the body type
        _logger.LogWarning("Get channel state keys not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(new List<string>());
    }

    public Task<bool> DeleteChannelStateKeyAsync(string channelName, string key)
    {
        // TODO: This requires CrossBar API enhancement to delete by key without knowing type
        // Current API has TryDeleteMessage<TBody>(channelName, key) which requires type parameter
        _logger.LogWarning("Delete channel state key not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(false);
    }

    public Task<bool> ResetChannelAsync(string channelName)
    {
        // TODO: This requires CrossBar API enhancement to reset without knowing type
        // Current API has ResetChannel<TBody>(channelName) which requires type parameter
        _logger.LogWarning("Reset channel not yet implemented - requires CrossBar API enhancement");
        return Task.FromResult(false);
    }

    private static SubscriptionInfoDto MapToSubscriptionInfoDto(CrossBar.SubscriptionInfo sub)
    {
        var stats = sub.Statistics.GetStats(false);
        return new SubscriptionInfoDto
        {
            Id = sub.Name,
            ChannelPattern = sub.ChannelName,
            IsWildcard = sub.IsWildcard,
            Status = "Active", // TODO: Detect suspended/detached status
            QueueDepth = stats.QueueLength,
            ProcessRate = stats.ProcessRate,
            AvgLatencyMs = stats.AvgLatencyTimeMs,
            PercentileLatencyMs = stats.PercentileLatencyTimeMs,
            TotalProcessed = stats.TotalProcessedMessages,
            TimeoutCount = stats.NumOfTimeouts,
            SubscribedOn = sub.SubscribedOn,
            ConflationInterval = sub.ConflationInterval == TimeSpan.Zero ? null : sub.ConflationInterval,
            ConflationRatio = sub.ConflationInterval == TimeSpan.Zero ? null : stats.ConflationRatio
        };
    }

    private static SubscriptionDetailDto MapToSubscriptionDetailDto(CrossBar.SubscriptionInfo sub)
    {
        var stats = sub.Statistics.GetStats(false);
        return new SubscriptionDetailDto
        {
            Id = sub.Name,
            ChannelPattern = sub.ChannelName,
            IsWildcard = sub.IsWildcard,
            Status = "Active", // TODO: Detect suspended/detached status
            QueueDepth = stats.QueueLength,
            ProcessRate = stats.ProcessRate,
            AvgLatencyMs = stats.AvgLatencyTimeMs,
            PercentileLatencyMs = stats.PercentileLatencyTimeMs,
            TotalProcessed = stats.TotalProcessedMessages,
            TimeoutCount = stats.NumOfTimeouts,
            SubscribedOn = sub.SubscribedOn,
            ConflationInterval = sub.ConflationInterval == TimeSpan.Zero ? null : sub.ConflationInterval,
            ConflationRatio = sub.ConflationInterval == TimeSpan.Zero ? null : stats.ConflationRatio,
            DequeueRate = stats.DequeueRate,
            TotalEnqueued = stats.TotalEnqueuedMessages,
            TotalDequeued = stats.TotalDequeuedMessages,
            AvgServiceTimeMs = stats.AvgServiceTimeMs,
            MinLatencyMs = stats.MinLatencyTimeMs,
            MaxLatencyMs = stats.MaxLatencyTimeMs,
            MinServiceTimeMs = stats.MinServiceTimeMs,
            MaxServiceTimeMs = stats.MaxServiceTimeMs,
            PercentileServiceTimeMs = stats.PercentileServiceTimeMs,
            AvgResponseTimeMs = stats.AvgResponseTime,
            LatencyToResponseRatio = stats.LatencyToResponseTimeRatio,
            EstimatedActiveMessages = stats.EstimatedAvgActiveMessages
        };
    }

    public Task<ConfigurationDto> GetConfigurationAsync()
    {
        try
        {
            var config = new ConfigurationDto
            {
                CrossBarOptions = new CrossBarConfigDto
                {
                    DefaultBufferCapacity = _options.DefaultBufferCapacity,
                    DefaultSlowConsumerStrategy = _options.DefaultSlowConsumerStrategy.ToString(),
                    DefaultConflationInterval = _options.DefaultConflationInterval.TotalMilliseconds > 0
                        ? _options.DefaultConflationInterval.ToString()
                        : "None",
                    MaxChannels = _options.MaxChannels,
                    MaxChannelNameLength = _options.MaxChannelNameLength,
                    EnableMessageTracing = _options.EnableMessageTracing,
                    EnableLifecycleTracking = _options.EnableLifecycleTracking,
                    EnablePublishLogging = _options.EnablePublishLogging,
                    SystemChannelPrefix = _options.SystemChannelPrefix,
                    SystemChannelBufferCapacity = _options.SystemChannelBufferCapacity
                },
                DefaultSubscriptionOptions = new SubscriptionOptionsDto
                {
                    HandlerTimeout = null,
                    HasTimeoutCallback = false
                },
                DefaultStatsOptions = new StatsOptionsDto
                {
                    Percentile = 0.99f,
                    Alpha = 0.05f,
                    Delta = 0.05f,
                    EwmaWindowSize = 50,
                    PercentileEnabled = true
                }
            };

            return Task.FromResult(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration");
            throw;
        }
    }
}
