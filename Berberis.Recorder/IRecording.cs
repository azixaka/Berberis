namespace Berberis.Recorder;

public interface IRecording : IDisposable
{
    bool Record();
    bool Pause();
}