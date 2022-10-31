namespace Berberis.Recorder;

public interface IPlayer : IDisposable
{
    Task Play();
    bool Pause();
}