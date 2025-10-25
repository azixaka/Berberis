using Berberis.Portal.Contracts.DTOs;

namespace Berberis.Portal.Api.Services;

/// <summary>Service for accessing CrossBar system information.</summary>
public interface IPortalService
{
    /// <summary>Gets system-wide overview statistics.</summary>
    Task<SystemOverviewDto> GetSystemOverviewAsync();

    /// <summary>Gets all channels with optional filtering.</summary>
    Task<List<ChannelInfoDto>> GetAllChannelsAsync(string? searchTerm = null);

    /// <summary>Gets detailed information about a specific channel.</summary>
    Task<ChannelDetailDto?> GetChannelDetailsAsync(string channelName);

    /// <summary>Gets all subscriptions with optional filtering.</summary>
    Task<List<SubscriptionInfoDto>> GetAllSubscriptionsAsync(string? searchTerm = null, string? statusFilter = null);

    /// <summary>Gets detailed information about a specific subscription.</summary>
    Task<SubscriptionDetailDto?> GetSubscriptionDetailsAsync(string subscriptionId);

    /// <summary>Suspends processing for a subscription.</summary>
    Task<bool> SuspendSubscriptionAsync(string subscriptionId);

    /// <summary>Resumes processing for a subscription.</summary>
    Task<bool> ResumeSubscriptionAsync(string subscriptionId);

    /// <summary>Detaches (removes) a subscription.</summary>
    Task<bool> DetachSubscriptionAsync(string subscriptionId);

    /// <summary>Gets state keys for a stateful channel.</summary>
    Task<List<string>> GetChannelStateKeysAsync(string channelName);

    /// <summary>Deletes a specific state key from a stateful channel.</summary>
    Task<bool> DeleteChannelStateKeyAsync(string channelName, string key);

    /// <summary>Resets a channel (clears message store if applicable).</summary>
    Task<bool> ResetChannelAsync(string channelName);

    /// <summary>Gets current system configuration.</summary>
    Task<ConfigurationDto> GetConfigurationAsync();
}
