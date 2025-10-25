using System.Text.Json;
using System.Text.Json.Serialization;

namespace Berberis.Recorder;

/// <summary>
/// Metadata describing a recording file.
/// </summary>
/// <remarks>
/// Metadata is stored in a separate .meta.json file alongside the recording file
/// (e.g., recording.rec + recording.rec.meta.json). This allows inspection of recording
/// properties without reading the entire binary file.
/// </remarks>
public sealed class RecordingMetadata
{
    /// <summary>
    /// When the recording was created (UTC).
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The channel that was recorded.
    /// </summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>
    /// The serializer type used for message bodies.
    /// </summary>
    [JsonPropertyName("serializerType")]
    public string? SerializerType { get; set; }

    /// <summary>
    /// The serializer version used for message bodies.
    /// </summary>
    [JsonPropertyName("serializerVersion")]
    public ushort SerializerVersion { get; set; }

    /// <summary>
    /// The message body type that was recorded.
    /// </summary>
    [JsonPropertyName("messageType")]
    public string? MessageType { get; set; }

    /// <summary>
    /// Total number of messages in the recording (if known).
    /// </summary>
    [JsonPropertyName("messageCount")]
    public long? MessageCount { get; set; }

    /// <summary>
    /// Timestamp (ticks) of the first message in the recording.
    /// </summary>
    [JsonPropertyName("firstMessageTicks")]
    public long? FirstMessageTicks { get; set; }

    /// <summary>
    /// Timestamp (ticks) of the last message in the recording.
    /// </summary>
    [JsonPropertyName("lastMessageTicks")]
    public long? LastMessageTicks { get; set; }

    /// <summary>
    /// Recording duration in milliseconds (derived from first/last message timestamps).
    /// </summary>
    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    /// <summary>
    /// Path to the index file, if this recording has been indexed (relative to recording file).
    /// </summary>
    [JsonPropertyName("indexFile")]
    public string? IndexFile { get; set; }

    /// <summary>
    /// Custom application-specific metadata (extensible).
    /// </summary>
    [JsonPropertyName("custom")]
    public Dictionary<string, string>? Custom { get; set; }

    /// <summary>
    /// Reads metadata from a .meta.json file.
    /// </summary>
    /// <param name="path">Path to the metadata file (e.g., "recording.rec.meta.json").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata, or null if the file does not exist.</returns>
    public static async Task<RecordingMetadata?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RecordingMetadata>(stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Writes metadata to a .meta.json file.
    /// </summary>
    /// <param name="metadata">The metadata to write.</param>
    /// <param name="path">Path to the metadata file (e.g., "recording.rec.meta.json").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(RecordingMetadata metadata, string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the conventional metadata file path for a recording file.
    /// </summary>
    /// <param name="recordingPath">Path to the recording file (e.g., "recording.rec").</param>
    /// <returns>Path to the metadata file (e.g., "recording.rec.meta.json").</returns>
    public static string GetMetadataPath(string recordingPath)
    {
        return $"{recordingPath}.meta.json";
    }
}
