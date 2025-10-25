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
    /// <remarks>
    /// Metadata is automatically created and populated with channel name, message type, serializer info, and timestamps.
    /// If the stream is a FileStream, metadata will be written to a .meta.json file alongside the recording.
    /// </remarks>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="crossBar">The CrossBar instance.</param>
    /// <param name="channel">The channel name to record.</param>
    /// <param name="stream">The stream to write recordings to.</param>
    /// <param name="serialiser">The serializer for message bodies.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A recording instance that can be disposed to stop recording.</returns>
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, CancellationToken token = default)
        => Record(crossBar, channel, stream, serialiser, false, TimeSpan.Zero, null, null, token);

    /// <summary>
    /// Records messages from a channel to a stream with options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Metadata is automatically created and populated with channel name, message type, serializer info, and timestamps.
    /// Use <paramref name="configureMetadata"/> to add custom fields.
    /// If the stream is a FileStream, metadata will be written to a .meta.json file alongside the recording.
    /// </para>
    /// <para>
    /// To enable streaming index building during recording, provide an <paramref name="indexStream"/>.
    /// The index stream must be writable and seekable. The recording stream must also be seekable for indexing.
    /// Index entries will be written incrementally as messages are recorded.
    /// </para>
    /// </remarks>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="crossBar">The CrossBar instance.</param>
    /// <param name="channel">The channel name to record.</param>
    /// <param name="stream">The stream to write recordings to.</param>
    /// <param name="serialiser">The serializer for message bodies.</param>
    /// <param name="saveInitialState">If true, records initial channel state.</param>
    /// <param name="conflationInterval">Conflation interval for message batching.</param>
    /// <param name="configureMetadata">Optional action to customize metadata. Metadata is pre-populated with known values; use this to add custom fields.</param>
    /// <param name="indexStream">Optional stream to write index to. If provided, both recording and index streams must be seekable.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A recording instance that can be disposed to stop recording.</returns>
    /// <exception cref="ArgumentException">Thrown if indexStream is provided but recording stream is not seekable.</exception>
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, bool saveInitialState, TimeSpan conflationInterval, Action<RecordingMetadata>? configureMetadata = null, Stream? indexStream = null, CancellationToken token = default)
    {
        // Auto-create and populate metadata with known values
        var metadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = channel,
            SerializerType = serialiser.GetType().Name,
            SerializerVersion = (ushort)serialiser.Version.Major,
            MessageType = typeof(TBody).Name,
            Custom = new Dictionary<string, string>()
        };

        // Let user customize (optional)
        configureMetadata?.Invoke(metadata);

        return Recording<TBody>.CreateRecording(crossBar, channel, stream, serialiser, saveInitialState, conflationInterval, metadata, indexStream, token);
    }
}
