using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>Message playback interface.</summary>
public interface IPlayer<TBody> : IDisposable
{
    /// <summary>Gets messages from the recording asynchronously.</summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>An async enumerable of recorded messages.</returns>
    IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token);

    /// <summary>Gets playback statistics.</summary>
    RecorderStats Stats { get; }
}