using Berberis.Messaging;

namespace Berberis.Recorder;

/// <summary>
/// Extension methods for recording CrossBar messages to a stream.
/// </summary>
public static class CrossBarExtensions
{
    /// <summary>
    /// Records messages from a channel to a stream.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="crossBar">The CrossBar instance.</param>
    /// <param name="channel">The channel name to record.</param>
    /// <param name="stream">The stream to write recordings to.</param>
    /// <param name="serialiser">The serializer for message bodies.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A recording instance that can be disposed to stop recording.</returns>
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, CancellationToken token = default)
        => Record(crossBar, channel, stream, serialiser, false, TimeSpan.Zero, null, token);

    /// <summary>
    /// Records messages from a channel to a stream with options.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="crossBar">The CrossBar instance.</param>
    /// <param name="channel">The channel name to record.</param>
    /// <param name="stream">The stream to write recordings to.</param>
    /// <param name="serialiser">The serializer for message bodies.</param>
    /// <param name="saveInitialState">If true, records initial channel state.</param>
    /// <param name="conflationInterval">Conflation interval for message batching.</param>
    /// <param name="metadata">Optional metadata to write to a .meta.json file. If the stream is a FileStream, metadata will be written to a .meta.json file alongside the recording file.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A recording instance that can be disposed to stop recording.</returns>
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, bool saveInitialState, TimeSpan conflationInterval, RecordingMetadata? metadata = null, CancellationToken token = default)
        => Recording<TBody>.CreateRecording(crossBar, channel, stream, serialiser, saveInitialState, conflationInterval, metadata, token);
}
