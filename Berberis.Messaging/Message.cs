namespace Berberis.Messaging;

/// <summary>Message envelope with metadata.</summary>
public struct Message<TBody>
{
    /// <summary>Unique message ID.</summary>
    public long Id { get; internal set; }
    /// <summary>UTC timestamp (binary format).</summary>
    public long Timestamp { get; }
    /// <summary>Message type (Update, Delete, Reset, Trace).</summary>
    public MessageType MessageType { get; internal set;}
    /// <summary>Correlation ID for related messages.</summary>
    public long CorrelationId { get; }
    /// <summary>State key for stateful channels.</summary>
    public string? Key { get; }
    internal long InceptionTicks { get; set; }
    /// <summary>Source identifier.</summary>
    public string? From { get; }
    /// <summary>Message payload.</summary>
    public TBody? Body { get; }
    /// <summary>Custom metadata tag.</summary>
    public long TagA { get; }
    /// <summary>Channel name where message was published.</summary>
    public string? ChannelName { get; internal set; }

    /// <summary>Creates message with metadata.</summary>
    public Message(long id, long timestamp, MessageType messageType, long correlationId, string? key, long inceptionTicks, string? from, TBody? body, long tagA, string? channelName = null)
    {
        Id = id;
        Timestamp = timestamp;
        MessageType = messageType;
        CorrelationId = correlationId;
        Key = key;
        InceptionTicks = inceptionTicks;
        From = from;
        Body = body;
        TagA = tagA;
        ChannelName = channelName;
    }
}
