namespace Berberis.Messaging.AspNetCore;

public interface IBerberisConsumer
{
    ISubscription Start(ICrossBar crossBar);
}
