namespace Berberis.Recorder;

public interface IPlayer : IDisposable
{
    bool Play();
    bool Pause();
}