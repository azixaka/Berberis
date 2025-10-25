namespace Berberis.Portal.Contracts.DTOs;

/// <summary>Channel information for API responses.</summary>
public class ChannelInfoDto
{
    /// <summary>Channel name.</summary>
    public required string Name { get; set; }

    /// <summary>Message body type name.</summary>
    public required string BodyType { get; set; }

    /// <summary>Publish rate (messages/second).</summary>
    public double PublishRate { get; set; }

    /// <summary>Total messages published.</summary>
    public long TotalMessages { get; set; }

    /// <summary>Number of subscriptions to this channel.</summary>
    public int SubscriptionCount { get; set; }

    /// <summary>Number of stored messages (0 if no message store).</summary>
    public int StoredMessageCount { get; set; }

    /// <summary>Last publish timestamp.</summary>
    public DateTime? LastPublishedAt { get; set; }

    /// <summary>Last publisher identifier.</summary>
    public string? LastPublishedBy { get; set; }
}
