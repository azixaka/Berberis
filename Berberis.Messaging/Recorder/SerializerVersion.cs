namespace Berberis.Recorder;

/// <summary>
/// Represents a serializer version with major and minor components.
/// </summary>
/// <remarks>
/// The serializer version is stored in the message header and allows the playback system
/// to detect version mismatches. Increment the major version for breaking changes to the
/// message format, and the minor version for backward-compatible additions.
/// </remarks>
/// <param name="Major">The major version number. Increment for breaking format changes.</param>
/// <param name="Minor">The minor version number. Increment for backward-compatible changes.</param>
public record struct SerializerVersion(byte Major, byte Minor);