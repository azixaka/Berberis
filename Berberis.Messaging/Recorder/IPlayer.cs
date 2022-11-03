using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

public interface IPlayer<TBody> : IDisposable
{
    IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token);
    RecorderStats Stats { get; }
}