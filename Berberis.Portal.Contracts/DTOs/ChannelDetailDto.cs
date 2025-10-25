namespace Berberis.Portal.Contracts.DTOs;

/// <summary>Detailed channel information including subscriptions.</summary>
public class ChannelDetailDto : ChannelInfoDto
{
    /// <summary>Subscriptions to this channel.</summary>
    public List<SubscriptionInfoDto> Subscriptions { get; set; } = new();

    /// <summary>Channel state keys (for stateful channels).</summary>
    public List<string>? StateKeys { get; set; }

    /// <summary>Total state size in bytes (if applicable).</summary>
    public long? StateSizeBytes { get; set; }
}
