namespace Berberis.Messaging;

/// <summary>Extension methods.</summary>
public static class Extensions
{
    /// <summary>Safely disposes object.</summary>
    public static bool TryDispose(this IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch
        {
            return false;
        }

        return true;
    }
}
