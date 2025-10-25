using Berberis.Messaging;
using Berberis.Messaging.Recorder;

namespace Berberis.Recorder;

/// <summary>
/// Message playback interface.
/// </summary>
/// <remarks>
/// <para><strong>Resource Ownership:</strong></para>
/// The player does not take ownership of the stream passed during creation.
/// The caller is responsible for disposing the stream when done.
///
/// <para><strong>Allocation Profile:</strong></para>
/// The playback implementation minimizes allocations:
/// <list type="bullet">
/// <item><description>Header buffers are pooled via ArrayPool (28 bytes per message, reused)</description></item>
/// <item><description>Key and From properties are cached after first access</description></item>
/// <item><description>Message bodies are deserialized according to the provided serializer's allocation characteristics</description></item>
/// </list>
/// </remarks>
public interface IPlayer<TBody> : IDisposable
{
    /// <summary>Gets messages from the recording asynchronously.</summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>An async enumerable of recorded messages.</returns>
    IAsyncEnumerable<Message<TBody>> MessagesAsync(CancellationToken token);

    /// <summary>Gets playback statistics.</summary>
    RecorderStats Stats { get; }
}