namespace Berberis.Recorder;

/// <summary>
/// Represents a serializer version with major and minor components.
/// </summary>
/// <param name="Major">The major version number.</param>
/// <param name="Minor">The minor version number.</param>
public record struct SerializerVersion(byte Major, byte Minor);