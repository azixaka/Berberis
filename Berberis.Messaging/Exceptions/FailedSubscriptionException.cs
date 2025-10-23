namespace Berberis.Messaging.Exceptions;

/// <summary>Failed subscription exception.</summary>
public sealed class FailedSubscriptionException : ApplicationException
{
    /// <summary>Creates exception.</summary>
    public FailedSubscriptionException() : base("Subscription failed") {}
}
