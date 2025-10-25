namespace Berberis.Recorder;

/// <summary>
/// Defines how recorded messages are played back.
/// </summary>
public enum PlayMode
{
    /// <summary>
    /// Play messages as fast as possible without delays.
    /// Messages are delivered immediately as they are read from the recording.
    /// </summary>
    AsFastAsPossible,

    /// <summary>
    /// Preserve the original timing between messages.
    /// Playback will delay between messages to match the intervals recorded during capture.
    /// Useful for replaying message flows with realistic timing.
    /// </summary>
    RespectOriginalMessageIntervals
}