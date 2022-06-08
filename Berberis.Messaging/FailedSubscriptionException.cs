namespace Berberis.Messaging;

public sealed class FailedSubscriptionException : ApplicationException
{
    public FailedSubscriptionException() : base("Subscription failed") {}
}
