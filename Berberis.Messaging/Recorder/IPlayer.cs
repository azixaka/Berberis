using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>
/// Message playback interface.
/// <para>
/// <strong>Resource Ownership:</strong> The player does not take ownership of the stream passed during creation.
/// The caller is responsible for disposing the stream when done.
/// </para>
/// </summary>
public interface IPlayer<TBody> : IDisposable
{
    /// <summary>Gets messages from the recording asynchronously.</summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>An async enumerable of recorded messages.</returns>
    IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token);

    /// <summary>Gets playback statistics.</summary>
    RecorderStats Stats { get; }
}