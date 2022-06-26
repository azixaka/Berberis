namespace Berberis.Messaging;

public readonly struct Message<TBody>
{
    public static readonly Message<TBody> Default = new Message<TBody>();

    public long Id { get; }
    public long Timestamp { get; }
    public long CorrelationId { get; }
    public string? Key { get; }
    internal long InceptionTicks { get; }
    public string? From { get; }
    public TBody Body { get; }

    public Message(long id, long timestamp, long correlationId, string? key, long inceptionTicks, string? from, TBody body)
    {
        Id = id;
        Timestamp = timestamp;
        CorrelationId = correlationId;
        Key = key;
        InceptionTicks = inceptionTicks;
        From = from;
        Body = body;
    }
}
