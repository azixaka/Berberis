using Berberis.Messaging;

namespace Berberis.Recorder;

/// <summary>
/// Indexed message playback interface with seeking capabilities.
/// </summary>
/// <remarks>
/// Indexed players support fast seeking to arbitrary positions in large recordings.
/// Requires an index file (.idx) created by <see cref="RecordingIndex.BuildAsync{TBody}"/>.
/// </remarks>
public interface IIndexedPlayer<TBody> : IPlayer<TBody>
{
    /// <summary>
    /// Gets the total number of messages in the recording (from index).
    /// </summary>
    long TotalMessages { get; }

    /// <summary>
    /// Seeks to the specified message number.
    /// </summary>
    /// <param name="messageNumber">The message number to seek to (0-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The actual message number seeked to (may be approximate due to index interval).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Message number is negative or beyond total messages.</exception>
    Task<long> SeekToMessageAsync(long messageNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeks to the first message at or after the specified timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp (ticks) to seek to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message number seeked to.</returns>
    Task<long> SeekToTimestampAsync(long timestamp, CancellationToken cancellationToken = default);
}
