namespace Berberis.Messaging;

public enum SlowConsumerStrategy : byte 
{
    SkipUpdates,
    FailSubscription,
    ConflateAndSkipUpdates // Can't be just conflate as messages without Key can't be conflated
}