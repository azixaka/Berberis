namespace Berberis.Messaging.Exceptions;

/// <summary>Invalid subscription exception.</summary>
public sealed class InvalidSubscriptionException : ApplicationException
{
    /// <summary>Creates exception with message.</summary>
    public InvalidSubscriptionException(string message) : base(message) {}
}
