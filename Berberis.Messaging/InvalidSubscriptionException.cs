namespace Berberis.Messaging;

public sealed class InvalidSubscriptionException : ApplicationException
{
    public InvalidSubscriptionException(string message) : base(message) {}
}
