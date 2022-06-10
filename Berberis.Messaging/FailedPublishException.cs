namespace Berberis.Messaging;

public sealed class FailedPublishException : ApplicationException
{
    public FailedPublishException(string message) : base(message) { }
}
