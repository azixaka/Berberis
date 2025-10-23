namespace Berberis.Recorder;

/// <summary>Message playback mode.</summary>
public enum PlayMode
{
    /// <summary>Play messages immediately.</summary>
    AsFastAsPossible,
    /// <summary>Preserve original message timing.</summary>
    RespectOriginalMessageIntervals
}