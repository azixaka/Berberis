using Berberis.Messaging;

namespace Berberis.Recorder;

public static class CrossBarExtensions
{
    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, string recordingName, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        var stream = File.OpenWrite($"{recordingName}.rec");
        return Recording<TBody>.CreateRecording(crossBar, channel, stream, saveInitialState, conflationInterval, token);
    }

    public static IRecording Record<TBody>(this ICrossBar crossBar, string channel, Stream outputStream, bool saveInitialState, TimeSpan conflationInterval, CancellationToken token = default)
    {
        return Recording<TBody>.CreateRecording(crossBar, channel, outputStream, saveInitialState, conflationInterval, token);
    }

    public static IPlayer Replay<TBody>(this ICrossBar crossBar, string channel, string recordingName, PlayMode playMode, CancellationToken token = default)
    {
        var stream = File.OpenRead($"{recordingName}.rec");
        return Player<TBody>.CreatePlayer(crossBar, channel, stream, playMode, token);
    }

    public static IPlayer Replay<TBody>(this ICrossBar crossBar, string channel, Stream inputStream, PlayMode playMode, CancellationToken token = default)
    {
        return Player<TBody>.CreatePlayer(crossBar, channel, inputStream, playMode, token);
    }
}
