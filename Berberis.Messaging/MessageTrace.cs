namespace Berberis.Messaging;

public enum OpType : byte { ChannelPublish, SubscriptionDequeue, SubscriptionProcessed }

public readonly struct MessageTrace
{
    public OpType OpType { get; init; }
    public string? MessageKey { get; init; }
    public string? From { get; init; }
    public long CorrelationId { get; init; }
    public string Channel { get; init; }
    public string SubscriptionName { get; init; }
    public long Ticks { get; init; }

    public override string ToString() =>
        $"[{OpType}] | msgKey:{MessageKey} | from:{From} | corId:{CorrelationId} | channel:{Channel} | subNam:{SubscriptionName} | ticks:{Ticks}";
}