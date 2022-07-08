using Berberis.Messaging;

namespace Berberis.Recorder;

public static class CrossBarExtensions
{
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, string recordingName, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        return Recording<TBody>.CreateRecording(crossBar, channel, recordingName, saveInitialState, conflationInterval, token);
    }

    public static IPlayer Replay<TBody>(this ICrossBar crossBar, string channel, string recordingName, PlayMode playMode, CancellationToken token = default)
    {
        return Player<TBody>.CreatePlayer(crossBar, channel, recordingName, playMode, token);
    }
}
