using Berberis.Messaging;

namespace Berberis.Recorder;

public interface IPlayer<TBody> : IDisposable
{
    IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token);
}