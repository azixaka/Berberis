namespace Berberis.Messaging;

public static class Extensions
{
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
