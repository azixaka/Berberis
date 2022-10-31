using Berberis.Messaging;

namespace Berberis.Recorder;

public static class CrossBarExtensions
{
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        return Recording<TBody>.CreateRecording(crossBar, channel, stream, serialiser, saveInitialState, conflationInterval, token);
    }

    public static IPlayer Replay<TBody>(this ICrossBar crossBar, string channel, Stream stream, IMessageBodySerializer<TBody> serialiser, PlayMode playMode, CancellationToken token = default)
    {
        return Player<TBody>.CreatePlayer(crossBar, channel, stream, serialiser, playMode, token);
    }
}
