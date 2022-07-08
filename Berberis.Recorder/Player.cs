using Berberis.Messaging;

namespace Berberis.Recorder;

internal sealed class Player<TBody> : IPlayer
{
    private Player() { }

    internal static IPlayer CreatePlayer(ICrossBar crossBar, string channel, string recordingName, PlayMode playMode, CancellationToken token = default)
    {
        var player = new Player<TBody>();
        return player;
    }

    public bool Play()
    {
        throw new NotImplementedException();
    }

    public bool Pause()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
