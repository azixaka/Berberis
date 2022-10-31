namespace Berberis.Recorder;

public interface IRecording : IDisposable
{
    Task MessageLoop { get; }
}