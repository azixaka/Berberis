using Berberis.Messaging;

namespace Berberis.Recorder;

internal sealed class Player<TBody> : IPlayer
{
    private Stream _stream;

    private Player() { }

    private void SetParams(Stream stream)
    {
        _stream = stream;
    }

    internal static IPlayer CreatePlayer(ICrossBar crossBar, string channel, Stream inputStream, PlayMode playMode, CancellationToken token = default)
    {
        var player = new Player<TBody>();
        player.SetParams(inputStream);
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
