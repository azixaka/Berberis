namespace Berberis.Recorder;

public interface IPlayer : IDisposable
{
    ValueTask<bool> Pause(CancellationToken token);
    bool Resume();

    Task MessageLoop { get; }
}