namespace Berberis.Messaging;

public struct Message<TBody>
{
    public long Id { get; internal set; }
    public long Timestamp { get; }
    public MessageType MessageType { get; internal set;}
    public long CorrelationId { get; }
    public string? Key { get; }
    internal long InceptionTicks { get; set; }
    public string? From { get; }
    public TBody? Body { get; }
    public long TagA { get; }

    public Message(long id, long timestamp, MessageType messageType, long correlationId, string? key, long inceptionTicks, string? from, TBody body, long tagA)
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
    }
}
