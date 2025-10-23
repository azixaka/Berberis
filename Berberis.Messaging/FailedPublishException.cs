namespace Berberis.Messaging;

/// <summary>Failed publish exception.</summary>
public sealed class FailedPublishException : ApplicationException
{
    /// <summary>Creates exception with message.</summary>
    public FailedPublishException(string message) : base(message) { }
}
