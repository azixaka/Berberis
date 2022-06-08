namespace Berberis.Messaging;

public readonly struct Message<TBody>
{
    public long Id { get; }
    public long Timestamp { get; }
    public long CorrelationId { get; }
    public string? Key { get; }
    internal long InceptionTicks { get; }
    public TBody Body { get; }

    public Message(long id, long timestamp, long correlationId, string? key, long inceptionTicks, TBody body)
    {
        Id = id;
        Timestamp = timestamp;
        CorrelationId = correlationId;
        Key = key;
        InceptionTicks = inceptionTicks;
        Body = body;
    }
}
